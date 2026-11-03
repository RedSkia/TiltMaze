using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TiltMaze.App.Services;

// Svar fra /api/auth/login og /api/auth/refresh
public class TokenDto
{
    public string AccessToken { get; set; } = "";
    public int ExpiresIn { get; set; } // sekunder
    public string RefreshToken { get; set; } = "";
}

// Svar fra GET /api/account
public class AccountDto
{
    public string Email { get; set; } = "";
    public string? Nickname { get; set; }
}

// Fejlsvar fra serveren: enten en titel eller en liste af valideringsfejl
public class ProblemDto
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

public class AuthResult
{
    public bool Ok { get; set; }
    public string Error { get; set; } = "";
}

// Login, konto og tokens. Adgangstokenet ligger kun i hukommelsen.
// Refresh-token, udløbstid, e-mail og kaldenavn ligger i SecureStorage, så man forbliver logget ind.
public class Auth
{
    private const int RefreshDays = 14; // samme som RefreshTokenExpiration på serveren

    private readonly HttpClient http = ApiSettings.CreateHttpClient();
    private readonly SemaphoreSlim refreshLock = new(1);
    private Task? loading;

    private string? accessToken;
    private DateTime accessExpires;
    private string? refreshToken;
    private DateTime refreshExpires;

    public string Email { get; private set; } = "";
    public string Nickname { get; private set; } = "";

    // Logget ind = vi har et refresh-token, der ikke er udløbet. Kræver ikke net
    public bool IsLoggedIn => refreshToken != null && refreshExpires > DateTime.UtcNow;

    // Sider lytter på denne for at vise login eller konto
    public event Action? Changed;

    // Læser det gemte login. Kan kaldes flere gange
    public Task Load()
    {
        loading ??= ReadStorage();
        return loading;
    }

    private async Task ReadStorage()
    {
        try
        {
            refreshToken = await SecureStorage.Default.GetAsync("refreshToken");
            var expires = await SecureStorage.Default.GetAsync("refreshExpires");
            Email = await SecureStorage.Default.GetAsync("email") ?? "";
            Nickname = await SecureStorage.Default.GetAsync("nickname") ?? "";
            if (!DateTime.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out refreshExpires))
                refreshToken = null;
        }
        catch (Exception)
        {
            // SecureStorage kan fejle på nogle enheder. Så skal brugeren bare logge ind igen
            refreshToken = null;
        }
    }

    // Opretter kontoen og logger ind med det samme
    public async Task<AuthResult> Register(string email, string password, string nickname)
    {
        using var response = await Send(() => http.PostAsJsonAsync("/api/account", new { email, password, nickname }));
        if (response == null)
            return NoConnection();
        if (!response.IsSuccessStatusCode)
            return new AuthResult { Error = await ReadError(response) };

        return await Login(email, password);
    }

    public async Task<AuthResult> Login(string email, string password)
    {
        using var response = await Send(() => http.PostAsJsonAsync("/api/auth/login", new { email, password }));
        if (response == null)
            return NoConnection();
        if (!response.IsSuccessStatusCode)
            return new AuthResult { Error = await ReadError(response) };

        var tokens = await response.Content.ReadFromJsonAsync<TokenDto>();
        if (tokens == null)
            return new AuthResult { Error = "The server sent an invalid response." };

        Email = email.Trim().ToLowerInvariant();
        await SaveTokens(tokens);
        await LoadNickname();
        Changed?.Invoke();
        return new AuthResult { Ok = true };
    }

    // Kaldenavnet hentes fra serveren efter login
    private async Task LoadNickname()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Send(() => http.SendAsync(request));
        if (response == null || !response.IsSuccessStatusCode)
            return;

        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        if (account != null)
            await SetNickname(account.Nickname ?? "");
    }

    // Gemmer kaldenavnet lokalt (kaldes efter login og når det er ændret på serveren)
    public async Task SetNickname(string nickname)
    {
        Nickname = nickname;
        try
        {
            await SecureStorage.Default.SetAsync("nickname", nickname);
        }
        catch (Exception)
        {
            // Kaldenavnet hentes igen ved næste login
        }
        Changed?.Invoke();
    }

    public void Logout()
    {
        accessToken = null;
        refreshToken = null;
        Email = "";
        Nickname = "";
        try
        {
            SecureStorage.Default.Remove("refreshToken");
            SecureStorage.Default.Remove("refreshExpires");
            SecureStorage.Default.Remove("email");
            SecureStorage.Default.Remove("nickname");
        }
        catch (Exception)
        {
            // Intet at rydde op, hvis SecureStorage ikke virker
        }
        Changed?.Invoke();
    }

    // Giver et gyldigt adgangstoken og fornyer det med refresh-token, når det er udløbet.
    // Returnerer null uden net, eller hvis man skal logge ind igen
    public async Task<string?> GetAccessToken(bool forceRefresh = false)
    {
        if (!forceRefresh && HasFreshAccessToken())
            return accessToken;
        if (!IsLoggedIn)
            return null;

        // Låsen sikrer, at to kald samtidig ikke fornyer to gange
        await refreshLock.WaitAsync();
        try
        {
            if (!forceRefresh && HasFreshAccessToken())
                return accessToken;
            return await Refresh();
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private bool HasFreshAccessToken()
    {
        return accessToken != null && accessExpires > DateTime.UtcNow.AddMinutes(1);
    }

    private async Task<string?> Refresh()
    {
        using var response = await Send(() => http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }));
        if (response == null)
            return null; // Ingen net. Vi beholder login og prøver senere

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Refresh-tokenet er ugyldigt eller udløbet: brugeren skal logge ind igen
            Logout();
            return null;
        }
        if (!response.IsSuccessStatusCode)
            return null;

        var tokens = await response.Content.ReadFromJsonAsync<TokenDto>();
        if (tokens == null)
            return null;
        await SaveTokens(tokens);
        return accessToken;
    }

    private async Task SaveTokens(TokenDto tokens)
    {
        accessToken = tokens.AccessToken;
        accessExpires = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
        refreshToken = tokens.RefreshToken;
        refreshExpires = DateTime.UtcNow.AddDays(RefreshDays);
        try
        {
            await SecureStorage.Default.SetAsync("refreshToken", refreshToken);
            await SecureStorage.Default.SetAsync("refreshExpires", refreshExpires.ToString("O", CultureInfo.InvariantCulture));
            await SecureStorage.Default.SetAsync("email", Email);
        }
        catch (Exception)
        {
            // Kan det ikke gemmes, er man logget ind, til appen lukkes
        }
    }

    // Sender til serveren. Returnerer null, hvis der ikke er forbindelse
    private static async Task<HttpResponseMessage?> Send(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
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

    private static AuthResult NoConnection()
    {
        return new AuthResult { Error = "Could not reach the server." };
    }

    // Laver serverens fejlsvar om til en besked
    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many attempts. Try again in a moment.";

        ProblemDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        }
        catch (Exception)
        {
            // Svaret var ikke JSON. Vi bruger standardbeskeden nedenfor
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (problem?.Detail == "LockedOut")
                return "The account is locked for a few minutes after too many attempts.";
            return "Wrong email or password.";
        }

        // Valideringsfejl (fx for kort kodeord): vis alle beskeder efter hinanden
        if (problem?.Errors != null && problem.Errors.Count > 0)
            return string.Join(". ", problem.Errors.Values.SelectMany(messages => messages));

        // Serverens egne tekster, fx "That nickname is already taken"
        if (response.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(problem?.Title))
            return problem.Title;

        return "Something went wrong on the server. Try again later.";
    }
}
