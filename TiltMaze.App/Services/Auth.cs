using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TiltMaze.App.Services;

// Svar fra /api/auth/login og /api/auth/refresh
public class TokenDto
{
    public string AccessToken { get; set; } = "";
    public int ExpiresIn { get; set; } // sekunder
    public string RefreshToken { get; set; } = "";
}

// Fejlsvar fra serveren: enten en titel eller en liste af valideringsfejl
public class ProblemDto
{
    public string? Title { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

public class AuthResult
{
    public bool Ok { get; set; }
    public string Error { get; set; } = "";
}

// Login, konto og tokens. Adgangstokenet ligger kun i hukommelsen.
// Refresh-token, udløbstid og brugernavn ligger kun i SecureStorage på enheden, så man forbliver logget ind.
// Er fingeraftryk slået til (Biometrics), starter appen låst, til fingeraftrykket eller kodeordet låser login op.
public class Auth
{
    private const int RefreshDays = 14; // samme som RefreshTokenExpiration på serveren

    private readonly HttpClient http = ApiSettings.CreateHttpClient();
    private readonly SemaphoreSlim refreshLock = new(1);
    private readonly Biometrics biometrics;
    private readonly GameDatabase database;
    private Task? loading;

    private string? accessToken;
    private DateTime accessExpires;
    private string? refreshToken;
    private DateTime refreshExpires;
    private bool locked; // der er et gemt login, men fingeraftryk skal låse det op

    public Auth(Biometrics biometrics, GameDatabase database)
    {
        this.biometrics = biometrics;
        this.database = database;
    }

    // Brugernavnet er også navnet på top-listen
    public string Username { get; private set; } = "";

    // Logget ind = vi har et refresh-token, der ikke er udløbet, og det er ikke låst. Kræver ikke net
    public bool IsLoggedIn => !locked && HasSavedLogin;

    // Låst = login findes på enheden, men kræver fingeraftryk (eller kodeord), før det bruges
    public bool IsLocked => locked && HasSavedLogin;

    private bool HasSavedLogin => refreshToken != null && refreshExpires > DateTime.UtcNow;

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
            Username = await SecureStorage.Default.GetAsync("username") ?? "";
            if (!DateTime.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out refreshExpires))
                refreshToken = null;

            // Er fingeraftryk slået til, starter appen låst
            locked = biometrics.Enabled;
        }
        catch (Exception)
        {
            // SecureStorage kan fejle på nogle enheder. Så skal brugeren bare logge ind igen
            refreshToken = null;
        }
    }

    // Kaldes, når fingeraftrykket er godkendt. Nu må det gemte login bruges
    public void Unlock()
    {
        locked = false;
    }

    // Opretter kontoen og logger ind med det samme
    public async Task<AuthResult> Register(string username, string password)
    {
        using var response = await Send(() => http.PostAsJsonAsync("/api/account", new { username, password }));
        if (response == null)
            return NoConnection();
        if (!response.IsSuccessStatusCode)
            return new AuthResult { Error = await ReadError(response) };

        return await Login(username, password);
    }

    public async Task<AuthResult> Login(string username, string password)
    {
        using var response = await Send(() => http.PostAsJsonAsync("/api/auth/login", new { username, password }));
        if (response == null)
            return NoConnection();
        if (!response.IsSuccessStatusCode)
            return new AuthResult { Error = await ReadError(response) };

        var tokens = await ReadJson<TokenDto>(response);
        if (tokens == null)
            return new AuthResult { Error = "The server sent an invalid response." };

        locked = false; // kodeordet er lige skrevet, så det nye login er låst op
        await SaveTokens(tokens);
        await SetUsername(username.Trim());
        await LoadUsername();
        return new AuthResult { Ok = true };
    }

    // Brugernavnet, som det er stavet på serveren (man kan logge ind med "marcus" på kontoen "Marcus")
    private async Task LoadUsername()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Send(() => http.SendAsync(request));
        if (response == null || !response.IsSuccessStatusCode)
            return;

        var account = await ReadJson<Dictionary<string, string>>(response);
        if (account != null && account.TryGetValue("username", out var name))
            await SetUsername(name);
    }

    // Er brugernavnet ledigt? null, hvis serveren ikke kunne spørges
    public async Task<bool?> IsAvailable(string username)
    {
        using var response = await Send(() => http.GetAsync("/api/account/available?username=" + Uri.EscapeDataString(username)));
        if (response == null || !response.IsSuccessStatusCode)
            return null;
        var answer = await ReadJson<Dictionary<string, bool>>(response);
        return answer != null && answer.TryGetValue("available", out bool available) ? available : null;
    }

    // Navnet er skiftet på serveren: flyt ventende rekorder med over på det nye navn, og gem det
    public async Task ChangeUsername(string username)
    {
        database.RenamePending(Username, username);
        await SetUsername(username);
    }

    // Gemmer brugernavnet lokalt (efter login og når det er ændret på serveren)
    public async Task SetUsername(string username)
    {
        Username = username;
        try
        {
            await SecureStorage.Default.SetAsync("username", username);
        }
        catch (Exception)
        {
            // Navnet gemmes igen ved næste login
        }
    }

    // Brugeren logger selv ud (eller sletter kontoen): ventende rekorder hører til den bruger og slettes,
    // så de ikke sendes under den næste, der logger ind
    public void Logout()
    {
        database.ClearPending();
        ClearLogin();
    }

    // Glemmer login'et, men beholder ventende rekorder
    private void ClearLogin()
    {
        accessToken = null;
        refreshToken = null;
        locked = false;
        Username = "";
        try
        {
            SecureStorage.Default.Remove("refreshToken");
            SecureStorage.Default.Remove("refreshExpires");
            SecureStorage.Default.Remove("username");
        }
        catch (Exception)
        {
            // Intet at rydde op, hvis SecureStorage ikke virker
        }
    }

    // Giver et gyldigt adgangstoken og fornyer det med refresh-token, når det er udløbet.
    // Returnerer null uden net, mens login er låst, eller hvis man skal logge ind igen
    public async Task<string?> GetAccessToken(bool forceRefresh = false)
    {
        if (!IsLoggedIn)
            return null;
        if (!forceRefresh && HasFreshAccessToken())
            return accessToken;

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
            ClearLogin();
            return null;
        }
        if (!response.IsSuccessStatusCode)
            return null;

        var tokens = await ReadJson<TokenDto>(response);
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

    private static AuthResult NoConnection()
    {
        return new AuthResult { Error = "Could not reach the server." };
    }

    // Laver serverens fejlsvar om til en besked
    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many attempts. Try again in a moment.";

        // Serveren siger det samme ved ukendt navn, forkert kodeord og låst konto
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Wrong username or password. After 5 wrong tries, wait 5 minutes.";

        ProblemDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        }
        catch (Exception)
        {
            // Svaret var ikke JSON. Vi bruger standardbeskeden nedenfor
        }

        // Valideringsfejl (fx for kort kodeord): vis alle beskeder efter hinanden
        if (problem?.Errors != null && problem.Errors.Count > 0)
            return string.Join(". ", problem.Errors.Values.SelectMany(messages => messages));

        // Serverens egne tekster, fx "That username is taken"
        if ((response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict) && !string.IsNullOrWhiteSpace(problem?.Title))
            return problem.Title;

        return "Something went wrong on the server. Try again later.";
    }
}
