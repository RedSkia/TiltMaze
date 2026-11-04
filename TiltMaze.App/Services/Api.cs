using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TiltMaze.Core;

namespace TiltMaze.App.Services;

// En linje på top-listen
public class LeaderboardRow
{
    public int Rank { get; set; }
    public int RunId { get; set; }
    public string Username { get; set; } = "";
    public int TimeMs { get; set; }
    public bool IsMe { get; set; }
}

// En andens bedste tur, som spøgelse man kan ræse mod
public class GhostRace
{
    public string Username { get; set; } = "";
    public int TimeMs { get; set; }
    public float[] Samples { get; set; } = [];
}

// Svar fra GET /api/runs/{id}/ghost
public class GhostDto
{
    public string Username { get; set; } = "";
    public int TimeMs { get; set; }
    public string Ghost { get; set; } = "";
}

// Hvad serveren svarede på en tid
public enum SendResult
{
    Sent,       // gemt på serveren
    Rejected,   // serveren vil aldrig tage imod den (fx tjekket fejlede), så den skal ikke prøves igen
    TryLater    // ingen net, ikke logget ind eller serveren har travlt
}

// Al tale med serveren, undtagen login (Auth). Sender adgangstoken med, og fornyer det hvis serveren svarer 401.
public class Api
{
    private readonly HttpClient http = ApiSettings.CreateHttpClient();
    private readonly Auth auth;

    public Api(Auth auth)
    {
        this.auth = auth;
    }

    // Sender en ny rekord
    public async Task<SendResult> SendRun(Difficulty difficulty, int seed, int timeMs, byte[] ghost)
    {
        var body = new { difficulty = difficulty.ToString(), seed, timeMs, ghost = Convert.ToBase64String(ghost) };
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Post, "/api/runs") { Content = JsonContent.Create(body) });
        if (response == null || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.TooManyRequests)
            return SendResult.TryLater;
        if (response.IsSuccessStatusCode)
            return SendResult.Sent;
        if ((int)response.StatusCode >= 500)
            return SendResult.TryLater;
        return SendResult.Rejected;
    }

    // Top-listen for en labyrint. Giver null, hvis den ikke kunne hentes (fx uden net)
    public async Task<List<LeaderboardRow>?> GetLeaderboard(Difficulty difficulty, int seed, int top = 10)
    {
        var url = "/api/leaderboard/" + difficulty + "/" + seed + "?top=" + top;
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, url));
        if (response == null || !response.IsSuccessStatusCode)
            return null;
        return await ReadJson<List<LeaderboardRow>>(response);
    }

    // De 3 bedste spøgelser på labyrinten, til at ræse mod. Ens egen tur springes over (den
    // har man lokalt). Tom liste uden net eller login
    public async Task<List<GhostRace>> GetTopGhosts(Difficulty difficulty, int seed)
    {
        var races = new List<GhostRace>();
        var rows = await GetLeaderboard(difficulty, seed, 3);
        if (rows == null)
            return races;

        foreach (var row in rows)
        {
            if (row.IsMe)
                continue;

            using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, "/api/runs/" + row.RunId + "/ghost"));
            if (response == null || !response.IsSuccessStatusCode)
                continue;
            var ghost = await ReadJson<GhostDto>(response);
            if (ghost == null)
                continue;
            races.Add(new GhostRace { Username = ghost.Username, TimeMs = ghost.TimeMs, Samples = Ghost.FromBytes(Convert.FromBase64String(ghost.Ghost)) });
        }
        return races;
    }

    // Ændrer brugernavnet (også navnet på top-listen). Giver en fejlbesked, eller "" hvis det lykkedes
    public async Task<string> SetUsername(string username)
    {
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Put, "/api/account/username")
        {
            Content = JsonContent.Create(new { username })
        });
        return await ErrorOf(response);
    }

    // Sletter hele kontoen på serveren (konto, tider og spøgelser). Giver en fejlbesked, eller "" hvis det lykkedes
    public async Task<string> DeleteAccount()
    {
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Delete, "/api/account"));
        return await ErrorOf(response);
    }

    // Sender med adgangstoken. Svarer serveren 401, fornyer vi tokenet og prøver én gang til.
    // Requesten laves af en funktion, fordi en request ikke kan sendes to gange.
    // Giver null, hvis der ikke er forbindelse
    private async Task<HttpResponseMessage?> Send(Func<HttpRequestMessage> makeRequest)
    {
        try
        {
            var response = await SendOnce(makeRequest, false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                response = await SendOnce(makeRequest, true);
            }
            return response;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendOnce(Func<HttpRequestMessage> makeRequest, bool refreshFirst)
    {
        var token = await auth.GetAccessToken(refreshFirst);
        using var request = makeRequest();
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request);
    }

    // Læser JSON fra svaret. Er svaret ikke JSON (fx fra en proxy), giver den null i stedet for en fejl
    private static async Task<T?> ReadJson<T>(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    // Fejlbesked ud fra svaret, eller "" hvis det lykkedes
    private static async Task<string> ErrorOf(HttpResponseMessage? response)
    {
        if (response == null)
            return "Could not reach the server.";
        if (response.IsSuccessStatusCode)
            return "";
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Sign in again.";
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many attempts. Try again in a moment.";

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return problem.Title;
        }
        catch (Exception)
        {
            // Svaret var ikke JSON. Vi bruger standardbeskeden nedenfor
        }
        return "Something went wrong on the server. Try again later.";
    }
}
