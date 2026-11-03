using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TiltMaze.Core;

namespace TiltMaze.Api.Tests;

public class EndpointTests : IDisposable
{
    const int Seed = 3;   // Easy-labyrint som computer-spilleren klarer

    readonly string dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
    readonly string keysPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    readonly WebApplicationFactory<Program> factory;

    public EndpointTests()
    {
        // Hver test får sin egen midlertidige database og nøglemappe
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:TiltMaze", "Data Source=" + dbPath);
            builder.UseSetting("DataProtection:KeysPath", keysPath);
        });
    }

    public void Dispose()
    {
        factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(dbPath);
        if (Directory.Exists(keysPath))
            Directory.Delete(keysPath, true);
    }

    record LoginResponse(string AccessToken, string RefreshToken);
    record Problem(string? Title);

    // Opretter en konto med kaldenavn, logger ind og giver en klient med adgangstokenet i headeren
    async Task<HttpClient> CreateUser(string nickname, string email)
    {
        var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/account", new { email, password = "kodeord123", nickname });
        Assert.Equal(HttpStatusCode.NoContent, create.StatusCode);
        return await Login(client, email);
    }

    static async Task<HttpClient> Login(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "kodeord123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    // En rigtig tur på Easy/Seed som body til POST /api/runs
    static object GenuineRun(double speed = 2)
    {
        var course = new Course(Difficulty.Easy, Seed);
        var player = Player.Play(course, 60, speed);
        Assert.True(player.Won);
        return new { difficulty = "Easy", seed = Seed, timeMs = player.TimeMs, ghost = Convert.ToBase64String(Ghost.ToBytes(player.Samples)) };
    }

    static async Task<string?> Title(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<Problem>())?.Title;
    }

    [Fact]
    public async Task RunsWithoutToken_Gives401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/runs", GenuineRun());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_WithBadNickname_Gives400()
    {
        var client = factory.CreateClient();

        foreach (var nickname in new[] { "ab", "this_name_is_way_too_long", "bad name", "bad-name", "" })
        {
            var response = await client.PostAsJsonAsync("/api/account", new { email = "a@example.com", password = "kodeord123", nickname });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task CreateAccount_WithShortPassword_Gives400WithEnglishText()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/account", new { email = "a@example.com", password = "kort", nickname = "Marcus" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at least 8 characters", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nickname_MustBeUniqueIgnoringCase()
    {
        await CreateUser("Marcus", "a@example.com");
        var other = factory.CreateClient();

        var response = await other.PostAsJsonAsync("/api/account", new { email = "b@example.com", password = "kodeord123", nickname = "marcus" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("That nickname is already taken", await Title(response));
    }

    [Fact]
    public async Task ChangeNickname_Works_AndTakenNameIsRejected()
    {
        var a = await CreateUser("Alice", "a@example.com");
        await CreateUser("Bob", "b@example.com");

        var taken = await a.PutAsJsonAsync("/api/account/nickname", new { nickname = "BOB" });
        var invalid = await a.PutAsJsonAsync("/api/account/nickname", new { nickname = "x" });
        var ok = await a.PutAsJsonAsync("/api/account/nickname", new { nickname = "Alice_2" });

        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        var account = await a.GetFromJsonAsync<Dictionary<string, string>>("/api/account");
        Assert.Equal("Alice_2", account!["nickname"]);
    }

    [Fact]
    public async Task AccountWithoutNickname_CannotSendRuns()
    {
        // /api/auth/register er Identity egen: den kender ikke kaldenavne
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { email = "a@example.com", password = "kodeord123" });
        await Login(client, "a@example.com");

        var response = await client.PostAsJsonAsync("/api/runs", GenuineRun());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Choose a nickname first", await Title(response));
    }

    [Fact]
    public async Task GenuineRun_IsAcceptedAndShowsOnTheLeaderboard()
    {
        var client = await CreateUser("Marcus", "marcus@example.com");

        var response = await client.PostAsJsonAsync("/api/runs", GenuineRun());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RunResponse>();
        Assert.True(result!.IsBest);
        Assert.Equal(1, result.Rank);

        var json = await client.GetStringAsync($"/api/leaderboard/Easy/{Seed}");
        Assert.DoesNotContain("marcus@example.com", json);
        var rows = await client.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Single(rows!);
        Assert.Equal("Marcus", rows![0].Nickname);
        Assert.Equal(result.TimeMs, rows[0].TimeMs);
        Assert.Equal(1, rows[0].Rank);
        Assert.True(rows[0].IsMe);
    }

    [Fact]
    public async Task OnlyTheBestRunIsKept()
    {
        var client = await CreateUser("Marcus", "marcus@example.com");

        var slow = await (await client.PostAsJsonAsync("/api/runs", GenuineRun(2))).Content.ReadFromJsonAsync<RunResponse>();
        var fast = await (await client.PostAsJsonAsync("/api/runs", GenuineRun(3))).Content.ReadFromJsonAsync<RunResponse>();
        var slowAgain = await (await client.PostAsJsonAsync("/api/runs", GenuineRun(2))).Content.ReadFromJsonAsync<RunResponse>();

        Assert.True(fast!.TimeMs < slow!.TimeMs);
        Assert.True(fast.IsBest);
        Assert.False(slowAgain!.IsBest);
        Assert.Equal(fast.TimeMs, slowAgain.TimeMs);
        var rows = await client.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Single(rows!);
        Assert.Equal(fast.TimeMs, rows![0].TimeMs);
    }

    [Fact]
    public async Task Leaderboard_IsSortedAndMarksOnlyMe_AndWorksWithoutLogin()
    {
        var slow = await CreateUser("Slow", "slow@example.com");
        var fast = await CreateUser("Fast", "fast@example.com");
        await slow.PostAsJsonAsync("/api/runs", GenuineRun(2));
        await fast.PostAsJsonAsync("/api/runs", GenuineRun(3));

        var asSlow = await slow.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        var anonymous = await factory.CreateClient().GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}?top=1");

        Assert.Equal(new[] { "Fast", "Slow" }, asSlow!.Select(r => r.Nickname));
        Assert.Equal(new[] { false, true }, asSlow!.Select(r => r.IsMe));
        Assert.Single(anonymous!);
        Assert.False(anonymous![0].IsMe);
    }

    [Fact]
    public async Task Leaderboard_ForOtherMaze_IsEmpty()
    {
        var client = await CreateUser("Marcus", "marcus@example.com");
        await client.PostAsJsonAsync("/api/runs", GenuineRun());

        var rows = await client.GetFromJsonAsync<List<LeaderboardRow>>("/api/leaderboard/Hard/" + Seed);

        Assert.Empty(rows!);
    }

    [Fact]
    public async Task Teleport_Gives400()
    {
        var client = await CreateUser("Cheater", "c@example.com");
        var course = new Course(Difficulty.Easy, Seed);
        var ghost = new float[] { course.Start.X + 0.5f, course.Start.Y + 0.5f, course.Goal.X + 0.5f, course.Goal.Y + 0.5f };

        var response = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 33, ghost = Convert.ToBase64String(Ghost.ToBytes(ghost)) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Run could not be verified", await Title(response));
        var rows = await client.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Empty(rows!);
    }

    [Fact]
    public async Task TooFastTimeForGenuineGhost_Gives400()
    {
        var client = await CreateUser("Cheater", "c@example.com");
        var course = new Course(Difficulty.Easy, Seed);
        var player = Player.Play(course);

        var response = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = player.TimeMs / 2, ghost = Convert.ToBase64String(Ghost.ToBytes(player.Samples)) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GhostForOtherMaze_Gives400()
    {
        var client = await CreateUser("Cheater", "c@example.com");
        var player = Player.Play(new Course(Difficulty.Easy, Seed));

        // Samme tur, men sendt som en anden labyrint: start og vægge passer ikke
        var response = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed + 1, timeMs = player.TimeMs, ghost = Convert.ToBase64String(Ghost.ToBytes(player.Samples)) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BadInput_Gives400()
    {
        var client = await CreateUser("Cheater", "c@example.com");

        var notBase64 = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = "%%%" });
        var empty = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = "" });
        var badDifficulty = await client.PostAsJsonAsync("/api/runs", new { difficulty = 99, seed = Seed, timeMs = 1000, ghost = "AAAA" });
        var negativeSeed = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = -5, timeMs = 1000, ghost = "AAAA" });

        Assert.Equal(HttpStatusCode.BadRequest, notBase64.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDifficulty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativeSeed.StatusCode);
    }

    [Fact]
    public async Task Runs_AreRateLimited()
    {
        var client = await CreateUser("Spammer", "s@example.com");
        HttpStatusCode last = HttpStatusCode.OK;

        for (int i = 0; i < 25; i++)
            last = (await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = "" })).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Ghost_CanBeFetchedForTopRuns()
    {
        var a = await CreateUser("Alice", "a@example.com");
        var b = await CreateUser("Bob", "b@example.com");
        var run = await (await a.PostAsJsonAsync("/api/runs", GenuineRun())).Content.ReadFromJsonAsync<RunResponse>();

        var response = await b.GetAsync($"/api/runs/{run!.Id}/ghost");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ghost = await response.Content.ReadFromJsonAsync<GhostResponse>();
        Assert.Equal("Alice", ghost!.Nickname);
        Assert.Equal(run.TimeMs, ghost.TimeMs);
        var samples = Ghost.FromBytes(Convert.FromBase64String(ghost.Ghost));
        Assert.Null(RunChecker.Check(new Course(Difficulty.Easy, Seed), run.TimeMs, samples));
    }

    [Fact]
    public async Task Ghost_NeedsLoginAndExistingRun()
    {
        var client = await CreateUser("Alice", "a@example.com");

        var missing = await client.GetAsync("/api/runs/999/ghost");
        var anonymous = await factory.CreateClient().GetAsync("/api/runs/1/ghost");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Ghost_OutsideTop10_IsOnlyForTheOwner()
    {
        var slow = await CreateUser("Slow", "slow@example.com");
        var other = await CreateUser("Other", "other@example.com");
        var run = await (await slow.PostAsJsonAsync("/api/runs", GenuineRun())).Content.ReadFromJsonAsync<RunResponse>();

        // 10 hurtigere ture direkte i databasen
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            for (int i = 0; i < 10; i++)
            {
                var user = new AppUser { UserName = "u" + i, Email = "u" + i + "@example.com", Nickname = "Fast" + i };
                db.Users.Add(user);
                db.Runs.Add(new RunEntity { UserId = user.Id, Difficulty = Difficulty.Easy, Seed = Seed, TimeMs = 1000 + i, Ghost = [], CreatedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/runs/{run!.Id}/ghost")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await slow.GetAsync($"/api/runs/{run.Id}/ghost")).StatusCode);
    }

    [Fact]
    public async Task Daily_GivesTodaysSeed()
    {
        var daily = await factory.CreateClient().GetFromJsonAsync<Dictionary<string, int>>("/api/daily");

        Assert.Equal(Course.DailySeed(), daily!["seed"]);
    }

    [Fact]
    public async Task DeleteAccount_RemovesRunsAndLogin()
    {
        var client = await CreateUser("Marcus", "marcus@example.com");
        await client.PostAsJsonAsync("/api/runs", GenuineRun());

        var delete = await client.DeleteAsync("/api/account");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var rows = await factory.CreateClient().GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Empty(rows!);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "marcus@example.com", password = "kodeord123" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutToken_Gives401()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_GivesNewTokens()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/account", new { email = "a@example.com", password = "kodeord123", nickname = "Alice" });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email = "a@example.com", password = "kodeord123" })).Content.ReadFromJsonAsync<LoginResponse>();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }
}
