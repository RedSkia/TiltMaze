using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using static TiltMaze.App.Services.ApiSettings;

namespace TiltMaze.App.Services;

// Svar fra Supabase ved opret, login og refresh
public class TokenDto
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; } // sekunder
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
}

// Fejlsvar: Supabase auth bruger msg, PostgREST message og edge function'en title
public class ErrorDto
{
    public string? Msg { get; set; }
    public string? Message { get; set; }
    public string? Title { get; set; }

    public string Text => Msg ?? Message ?? Title ?? "";
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
    private const int RefreshDays = 14; // så længe gemmer vi refresh-tokenet på enheden

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

    // Opretter kontoen. Supabase logger ind med det samme (e-mailbekræftelse er slået fra)
    public async Task<AuthResult> Register(string username, string password)
    {
        username = username.Trim();
        var body = new { email = ApiSettings.EmailOf(username), password, data = new { username } };
        using var response = await TrySend(() => http.PostAsJsonAsync("/auth/v1/signup", body));
        if (response == null)
            return NoConnection();
        if (!response.IsSuccessStatusCode)
            return new AuthResult { Error = await ReadError(response, false) };

        return await TakeLogin(response, username);
    }

    public async Task<AuthResult> Login(string username, string password)
    {
        username = username.Trim();
        var body = new { email = ApiSettings.EmailOf(username), password };
        using var response = await TrySend(() => http.PostAsJsonAsync("/auth/v1/token?grant_type=password", body));
        if (response == null)
            return NoConnection();
        if (!response.IsSuccessStatusCode)
            return new AuthResult { Error = await ReadError(response, true) };

        return await TakeLogin(response, username);
    }

    // Gemmer tokens og brugernavn fra et godkendt svar
    private async Task<AuthResult> TakeLogin(HttpResponseMessage response, string username)
    {
        var tokens = await ReadJson<TokenDto>(response);
        if (tokens == null)
            return new AuthResult { Error = "The server sent an invalid response." };

        locked = false; // kodeordet er lige skrevet, så det nye login er låst op
        await SaveTokens(tokens);
        await SetUsername(username);
        return new AuthResult { Ok = true };
    }

    // Er brugernavnet ledigt? null, hvis serveren ikke kunne spørges
    public async Task<bool?> IsAvailable(string username)
    {
        using var response = await TrySend(() => http.PostAsJsonAsync("/rest/v1/rpc/username_available", new { p_username = username }));
        if (response == null || !response.IsSuccessStatusCode)
            return null;
        return await ReadJson<bool?>(response);
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
        using var response = await TrySend(() => http.PostAsJsonAsync("/auth/v1/token?grant_type=refresh_token", new { refresh_token = refreshToken }));
        if (response == null)
            return null; // Ingen net. Vi beholder login og prøver senere

        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized)
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

    private static AuthResult NoConnection()
    {
        return new AuthResult { Error = "Could not reach the server." };
    }

    // Laver serverens fejlsvar om til en besked. Ved login er alle fejl ens (ingen afsløring af navne)
    private static async Task<string> ReadError(HttpResponseMessage response, bool isLogin)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Too many attempts. Try again in a moment.";

        if (isLogin && (int)response.StatusCode < 500)
            return "Wrong username or password";

        ErrorDto? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        }
        catch (Exception)
        {
            // Svaret var ikke JSON. Vi bruger standardbeskeden nedenfor
        }
        var text = error?.Text ?? "";

        // Dobbelt navn giver en fejl, hvor teksten har "taken" eller "already"
        if (text.Contains("taken", StringComparison.OrdinalIgnoreCase) || text.Contains("already", StringComparison.OrdinalIgnoreCase))
            return "That username is taken";

        // Fx et for kort kodeord
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity && text != "")
            return text;

        return "Something went wrong on the server. Try again later.";
    }
}
