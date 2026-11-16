using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TiltMaze.Core;
using static TiltMaze.App.Services.ApiSettings;

namespace TiltMaze.App.Services;

// En linje på top-listen
public class LeaderboardRow
{
    public int Rank { get; set; }
    [JsonPropertyName("run_id")] public int RunId { get; set; }
    public string Username { get; set; } = "";
    [JsonPropertyName("time_ms")] public int TimeMs { get; set; }
    [JsonPropertyName("is_me")] public bool IsMe { get; set; }
}

// Svar fra top_ghosts: en andens bedste tur (ghost er base64)
public class GhostDto
{
    public string Username { get; set; } = "";
    [JsonPropertyName("time_ms")] public int TimeMs { get; set; }
    public string Ghost { get; set; } = "";
}

// En andens bedste tur, som spøgelse man kan ræse mod
public class GhostRace
{
    public string Username { get; set; } = "";
    public int TimeMs { get; set; }
    public float[] Samples { get; set; } = [];
}

// Svar fra submit-run: pladsen på top-listen for brugerens bedste tid
public class RunReply
{
    public int Rank { get; set; }
}

// Hvad serveren svarede på en tid
public enum SendResult
{
    Sent,       // gemt på serveren
    Rejected,   // serveren vil aldrig tage imod den (fx tjekket fejlede), så den skal ikke prøves igen
    TryLater    // ingen net, ikke logget ind eller serveren har travlt
}

// Al tale med Supabase, undtagen login (Auth). Sender adgangstoken med, og fornyer det hvis serveren svarer 401.
public class Api
{
    private readonly HttpClient http = ApiSettings.CreateHttpClient();
    private readonly Auth auth;

    public Api(Auth auth)
    {
        this.auth = auth;
    }

    // Sender en ny rekord. Ved Sent er Rank pladsen på top-listen (ellers 0)
    public async Task<(SendResult Result, int Rank)> SendRun(Difficulty difficulty, int seed, int timeMs, byte[] ghost)
    {
        var body = new { difficulty = (int)difficulty, seed, timeMs, ghost = Convert.ToBase64String(ghost) };
        using var response = await Post("/functions/v1/submit-run", body);
        if (response == null || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.TooManyRequests)
            return (SendResult.TryLater, 0);
        if (response.IsSuccessStatusCode)
        {
            var reply = await ReadJson<RunReply>(response);
            return (SendResult.Sent, reply?.Rank ?? 0);
        }
        if ((int)response.StatusCode >= 500)
            return (SendResult.TryLater, 0);
        return (SendResult.Rejected, 0);
    }

    // Top-listen for en labyrint. Giver null, hvis den ikke kunne hentes (fx uden net)
    public async Task<List<LeaderboardRow>?> GetLeaderboard(Difficulty difficulty, int seed, int top = 10)
    {
        var body = new { p_difficulty = (int)difficulty, p_seed = seed, p_top = top };
        using var response = await Post("/rest/v1/rpc/leaderboard", body);
        if (response == null || !response.IsSuccessStatusCode)
            return null;
        return await ReadJson<List<LeaderboardRow>>(response);
    }

    // De 3 bedste spøgelser på labyrinten, til at ræse mod (ens egen tur er ikke med).
    // Tom liste uden net
    public async Task<List<GhostRace>> GetTopGhosts(Difficulty difficulty, int seed)
    {
        var races = new List<GhostRace>();
        var body = new { p_difficulty = (int)difficulty, p_seed = seed };
        using var response = await Post("/rest/v1/rpc/top_ghosts", body);
        if (response == null || !response.IsSuccessStatusCode)
            return races;

        var ghosts = await ReadJson<List<GhostDto>>(response) ?? [];
        foreach (var ghost in ghosts)
            races.Add(new GhostRace { Username = ghost.Username, TimeMs = ghost.TimeMs, Samples = Ghost.FromBytes(Convert.FromBase64String(ghost.Ghost)) });
        return races;
    }

    // Ændrer brugernavnet (også navnet på top-listen). Giver en fejlbesked, eller "" hvis det lykkedes
    public async Task<string> SetUsername(string username)
    {
        using var response = await Post("/rest/v1/rpc/set_username", new { p_username = username });
        return await ErrorOf(response);
    }

    // Sletter hele kontoen på serveren (konto, tider og spøgelser). Giver en fejlbesked, eller "" hvis det lykkedes
    public async Task<string> DeleteAccount()
    {
        using var response = await Post("/rest/v1/rpc/delete_me", new { });
        return await ErrorOf(response);
    }

    // POST med JSON. Giver null, hvis der ikke er forbindelse
    private Task<HttpResponseMessage?> Post(string url, object body)
    {
        return Send(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) });
    }

    // Sender med adgangstoken. Svarer serveren 401, fornyer vi tokenet og prøver én gang til.
    // Requesten laves af en funktion, fordi en request ikke kan sendes to gange.
    // Giver null, hvis der ikke er forbindelse
    private Task<HttpResponseMessage?> Send(Func<HttpRequestMessage> makeRequest)
    {
        return TrySend(async () =>
        {
            var response = await SendOnce(makeRequest, false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                response = await SendOnce(makeRequest, true);
            }
            return response;
        });
    }

    private async Task<HttpResponseMessage> SendOnce(Func<HttpRequestMessage> makeRequest, bool refreshFirst)
    {
        var token = await auth.GetAccessToken(refreshFirst);
        using var request = makeRequest();
        // Uden login bliver anon-nøglen sendt som token (står i HttpClient'ens standard-headers)
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request);
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

        var error = await ReadJson<ErrorDto>(response);
        if (error != null && error.Text.Contains("taken", StringComparison.OrdinalIgnoreCase))
            return "That username is taken";
        return "Something went wrong on the server. Try again later.";
    }
}
