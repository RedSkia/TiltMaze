using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TiltMaze.Core;

namespace TiltMaze.Api.Tests;

public class EndpointTests : IDisposable
{
    const int Seed = 3;   // Easy-labyrint som computer-spilleren klarer
    const string Password = "kodeord123";

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

    record LoginResponse(string AccessToken, int ExpiresIn, string RefreshToken);
    record Problem(string? Title);
    record Available(bool IsAvailable);

    // Opretter en konto, logger ind og giver en klient med adgangstokenet i headeren
    async Task<HttpClient> CreateUser(string username, WebApplicationFactory<Program>? app = null)
    {
        var client = (app ?? factory).CreateClient();
        var create = await client.PostAsJsonAsync("/api/account", new { username, password = Password });
        Assert.Equal(HttpStatusCode.NoContent, create.StatusCode);
        return await Login(client, username);
    }

    static async Task<HttpClient> Login(HttpClient client, string username)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
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

    async Task<bool> IsAvailable(string username)
    {
        var json = await factory.CreateClient().GetFromJsonAsync<Dictionary<string, bool>>("/api/account/available?username=" + username);
        return json!["available"];
    }

    // ---------- Konto og login ----------

    [Fact]
    public async Task CreateAccount_WithBadUsername_Gives400()
    {
        var client = factory.CreateClient();

        foreach (var username in new[] { "ab", "this_name_is_way_too_long", "bad name", "bad-name", "a@example.com", "" })
        {
            var response = await client.PostAsJsonAsync("/api/account", new { username, password = Password });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task CreateAccount_WithShortPassword_Gives400WithEnglishText()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/account", new { username = "Marcus", password = "kort" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at least 8 characters", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Username_MustBeUniqueIgnoringCase()
    {
        await CreateUser("Marcus");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/account", new { username = "mARCUS", password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("That username is taken", await Title(response));
    }

    [Fact]
    public async Task Available_SaysIfTheUsernameIsFree()
    {
        await CreateUser("Marcus");

        Assert.False(await IsAvailable("Marcus"));
        Assert.False(await IsAvailable("MARCUS"));
        Assert.True(await IsAvailable("Someone_2"));
        var invalid = await factory.CreateClient().GetAsync("/api/account/available?username=a");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Available_IsRateLimited()
    {
        var client = factory.CreateClient();
        HttpStatusCode last = HttpStatusCode.OK;

        for (int i = 0; i < 35; i++)
            last = (await client.GetAsync("/api/account/available?username=Name" + i)).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Login_WithUsername_IgnoresCase()
    {
        await CreateUser("Marcus");

        var client = await Login(factory.CreateClient(), "marcus");
        var account = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/account");

        Assert.Equal("Marcus", account!["username"]);
    }

    [Fact]
    public async Task Login_Failures_AllGiveTheSameAnswer()
    {
        await CreateUser("Marcus");
        var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { username = "Marcus", password = "forkert123" });
        var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new { username = "Nobody", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal("Wrong username or password", await Title(wrongPassword));
        Assert.Equal("Wrong username or password", await Title(unknownUser));
    }

    [Fact]
    public async Task Login_LocksTheAccountAfter5WrongPasswords()
    {
        // Rate limit på login slås næsten fra i denne test, så det er låsen og ikke rate limit, vi måler
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimits:Auth", "1000"));
        await CreateUser("Marcus", app);
        var client = app.CreateClient();

        for (int i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login", new { username = "Marcus", password = "forkert123" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        var locked = await client.PostAsJsonAsync("/api/auth/login", new { username = "Marcus", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("Wrong username or password", await Title(locked));
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByNameAsync("Marcus"))!));
    }

    [Fact]
    public async Task Login_IsRateLimitedPerIp()
    {
        var client = factory.CreateClient();
        HttpStatusCode last = HttpStatusCode.OK;

        for (int i = 0; i < 25; i++)
            last = (await client.PostAsJsonAsync("/api/auth/login", new { username = "Nobody" + i, password = Password })).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task OldEmailRoutes_AreGone()
    {
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email = "a@example.com", password = Password });
        var forgot = await client.PostAsJsonAsync("/api/auth/forgotPassword", new { email = "a@example.com" });

        Assert.Equal(HttpStatusCode.NotFound, register.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forgot.StatusCode);
    }

    [Fact]
    public async Task ChangeUsername_Works_AndTakenNameIsRejected()
    {
        var a = await CreateUser("Alice");
        await CreateUser("Bob");

        var taken = await a.PutAsJsonAsync("/api/account/username", new { username = "BOB" });
        var invalid = await a.PutAsJsonAsync("/api/account/username", new { username = "x" });
        var sameWithOtherCase = await a.PutAsJsonAsync("/api/account/username", new { username = "ALICE" });
        var ok = await a.PutAsJsonAsync("/api/account/username", new { username = "Alice_2" });

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, sameWithOtherCase.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        var account = await a.GetFromJsonAsync<Dictionary<string, string>>("/api/account");
        Assert.Equal("Alice_2", account!["username"]);
        Assert.True(await IsAvailable("Alice"));
        await Login(factory.CreateClient(), "Alice_2");

        // Tokenet gælder stadig, og top-listen viser det nye navn
        await a.PostAsJsonAsync("/api/runs", GenuineRun());
        var rows = await a.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Equal("Alice_2", rows![0].Username);
    }

    [Fact]
    public async Task AccountEndpoints_NeedLogin()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/account/username", new { username = "Marcus" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task Refresh_GivesNewTokens_AndBadTokenGives401()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/account", new { username = "Alice", password = Password });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { username = "Alice", password = Password })).Content.ReadFromJsonAsync<LoginResponse>();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login!.RefreshToken });
        var bad = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-token" });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var tokens = await refresh.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(tokens!.AccessToken));
        Assert.Equal(3600, tokens.ExpiresIn);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task TokenKeys_ArePersistedInTheKeysFolder()
    {
        await CreateUser("Marcus");

        Assert.NotEmpty(Directory.GetFiles(keysPath, "*.xml"));
    }

    [Fact]
    public async Task DeleteAccount_RemovesRunsGhostsAndLogin()
    {
        var client = await CreateUser("Marcus");
        var other = await CreateUser("Other");
        var run = await (await client.PostAsJsonAsync("/api/runs", GenuineRun())).Content.ReadFromJsonAsync<RunResponse>();
        var login = await (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "Marcus", password = Password })).Content.ReadFromJsonAsync<LoginResponse>();

        var delete = await client.DeleteAsync("/api/account");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var rows = await factory.CreateClient().GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Empty(rows!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/runs/{run!.Id}/ghost")).StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Empty(scope.ServiceProvider.GetRequiredService<GameDb>().Runs);
        var loginAgain = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "Marcus", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAgain.StatusCode);
        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.True(await IsAvailable("Marcus"));
    }

    // ---------- Fejl og sikkerhed ----------

    [Fact]
    public async Task BrokenJson_GivesShortProblemWithoutDetails()
    {
        var content = new StringContent("{ \"username\": ", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateClient().PostAsync("/api/account", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain(" at ", body);
    }

    [Fact]
    public async Task Cors_IsNotOpened()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/daily");
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // ---------- Tider og top-liste ----------

    [Fact]
    public async Task RunsWithoutToken_Gives401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/runs", GenuineRun());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenuineRun_IsAcceptedAndShowsUsernameOnTheLeaderboard()
    {
        var client = await CreateUser("Marcus");

        var response = await client.PostAsJsonAsync("/api/runs", GenuineRun());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RunResponse>();
        Assert.True(result!.IsBest);
        Assert.Equal(1, result.Rank);

        var rows = await client.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        Assert.Single(rows!);
        Assert.Equal("Marcus", rows![0].Username);
        Assert.Equal(result.TimeMs, rows[0].TimeMs);
        Assert.Equal(1, rows[0].Rank);
        Assert.True(rows[0].IsMe);
    }

    [Fact]
    public async Task OnlyTheBestRunIsKept()
    {
        var client = await CreateUser("Marcus");

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

    // Appen giver mønter ud fra rank i svaret, så den skal være pladsen for brugerens bedste tid
    [Fact]
    public async Task RunResponse_RankIsThePlaceOfTheUsersBestRun()
    {
        var slow = await CreateUser("Slow");
        var fast = await CreateUser("Fast");

        var first = await (await slow.PostAsJsonAsync("/api/runs", GenuineRun(2))).Content.ReadFromJsonAsync<RunResponse>();
        var second = await (await fast.PostAsJsonAsync("/api/runs", GenuineRun(3))).Content.ReadFromJsonAsync<RunResponse>();
        var slowAgain = await (await slow.PostAsJsonAsync("/api/runs", GenuineRun(2))).Content.ReadFromJsonAsync<RunResponse>();

        Assert.Equal(1, first!.Rank);
        Assert.Equal(1, second!.Rank);
        Assert.Equal(2, slowAgain!.Rank);
    }

    [Fact]
    public async Task Leaderboard_IsSortedAndMarksOnlyMe_AndWorksWithoutLogin()
    {
        var slow = await CreateUser("Slow");
        var fast = await CreateUser("Fast");
        await slow.PostAsJsonAsync("/api/runs", GenuineRun(2));
        await fast.PostAsJsonAsync("/api/runs", GenuineRun(3));

        var asSlow = await slow.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}");
        var anonymous = await factory.CreateClient().GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/Easy/{Seed}?top=1");

        Assert.Equal(new[] { "Fast", "Slow" }, asSlow!.Select(r => r.Username));
        Assert.Equal(new[] { false, true }, asSlow!.Select(r => r.IsMe));
        Assert.Single(anonymous!);
        Assert.False(anonymous![0].IsMe);
    }

    [Fact]
    public async Task Leaderboard_ForOtherMaze_IsEmpty()
    {
        var client = await CreateUser("Marcus");
        await client.PostAsJsonAsync("/api/runs", GenuineRun());

        var rows = await client.GetFromJsonAsync<List<LeaderboardRow>>("/api/leaderboard/Hard/" + Seed);

        Assert.Empty(rows!);
    }

    [Fact]
    public async Task Teleport_Gives400()
    {
        var client = await CreateUser("Cheater");
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
        var client = await CreateUser("Cheater");
        var course = new Course(Difficulty.Easy, Seed);
        var player = Player.Play(course);

        var response = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = player.TimeMs / 2, ghost = Convert.ToBase64String(Ghost.ToBytes(player.Samples)) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GhostForOtherMaze_Gives400()
    {
        var client = await CreateUser("Cheater");
        var player = Player.Play(new Course(Difficulty.Easy, Seed));

        // Samme tur, men sendt som en anden labyrint: start og vægge passer ikke
        var response = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed + 1, timeMs = player.TimeMs, ghost = Convert.ToBase64String(Ghost.ToBytes(player.Samples)) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BadInput_Gives400()
    {
        var client = await CreateUser("Cheater");

        var notBase64 = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = "%%%" });
        var empty = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = "" });
        var oddLength = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = Convert.ToBase64String(new byte[12]) });
        var badDifficulty = await client.PostAsJsonAsync("/api/runs", new { difficulty = 99, seed = Seed, timeMs = 1000, ghost = "AAAA" });
        var negativeSeed = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = -5, timeMs = 1000, ghost = "AAAA" });

        Assert.Equal(HttpStatusCode.BadRequest, notBase64.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oddLength.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDifficulty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativeSeed.StatusCode);
    }

    [Fact]
    public async Task TooLongGhost_Gives400()
    {
        var client = await CreateUser("Cheater");
        var ghost = Convert.ToBase64String(new byte[(RunChecker.MaxSamples + 1) * 8]);

        var response = await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Runs_AreRateLimited()
    {
        var client = await CreateUser("Spammer");
        HttpStatusCode last = HttpStatusCode.OK;

        for (int i = 0; i < 25; i++)
            last = (await client.PostAsJsonAsync("/api/runs", new { difficulty = "Easy", seed = Seed, timeMs = 1000, ghost = "" })).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Ghost_CanBeFetchedForTopRuns()
    {
        var a = await CreateUser("Alice");
        var b = await CreateUser("Bob");
        var run = await (await a.PostAsJsonAsync("/api/runs", GenuineRun())).Content.ReadFromJsonAsync<RunResponse>();

        var response = await b.GetAsync($"/api/runs/{run!.Id}/ghost");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ghost = await response.Content.ReadFromJsonAsync<GhostResponse>();
        Assert.Equal("Alice", ghost!.Username);
        Assert.Equal(run.TimeMs, ghost.TimeMs);
        var samples = Ghost.FromBytes(Convert.FromBase64String(ghost.Ghost));
        Assert.Null(RunChecker.Check(new Course(Difficulty.Easy, Seed), run.TimeMs, samples));
    }

    [Fact]
    public async Task Ghost_NeedsLoginAndExistingRun()
    {
        var client = await CreateUser("Alice");

        var missing = await client.GetAsync("/api/runs/999/ghost");
        var anonymous = await factory.CreateClient().GetAsync("/api/runs/1/ghost");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Ghost_OutsideTop10_IsOnlyForTheOwner()
    {
        var slow = await CreateUser("Slow");
        var other = await CreateUser("Other");
        var run = await (await slow.PostAsJsonAsync("/api/runs", GenuineRun())).Content.ReadFromJsonAsync<RunResponse>();

        // 10 hurtigere ture direkte i databasen
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            for (int i = 0; i < 10; i++)
            {
                var user = new IdentityUser { UserName = "Fast" + i, NormalizedUserName = "FAST" + i };
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
}
