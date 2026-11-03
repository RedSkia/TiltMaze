using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TiltMaze.Api;
using TiltMaze.Core;

const int MaxGhostBytes = RunChecker.MaxSamples * 8;   // 8 bytes pr. måling (x og y)
const int MaxRequestBytes = MaxGhostBytes * 4 / 3 + 1024;   // base64 er en tredjedel større end bytes
const int TopGhosts = 10;                      // spøgelset kan hentes for de 10 bedste (og ens egne)
const string WrongLogin = "Wrong username or password";   // samme tekst for ukendt bruger, forkert kodeord og låst konto

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<GameDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TiltMaze")));

// Brugere: Identity gemmer kodeord som hash og giver bearer-tokens. Brugernavnet er også navnet på top-listen. Ingen e-mail
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
{
    options.User.RequireUniqueEmail = false;
    options.User.AllowedUserNameCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;   // derefter er kontoen låst i 5 minutter
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddErrorDescriber<IdentityErrors>()
.AddEntityFrameworkStores<GameDb>();
builder.Services.AddAuthorization();

// Adgangstoken gælder 1 time, refresh-token 14 dage
builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromHours(1);
    options.RefreshTokenExpiration = TimeSpan.FromDays(14);
});

// Nøglerne som krypterer tokens gemmes i en mappe, så ingen bliver logget ud ved ny container (i Docker: volumen /data)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "keys"));

builder.Services.AddProblemDetails();

// Sværhedsgraden kan sendes som tekst ("Normal") eller tal
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Rate limits: tider og læsning pr. bruger, login, oprettelse og "er navnet ledigt" pr. IP-adresse.
// Grænsen for login kan kun ændres i konfigurationen (testen af lås efter 5 forkerte kodeord bruger det)
int authLimit = builder.Configuration.GetValue("RateLimits:Auth", 20);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("runs", context => PerMinute(UserOrIp(context), 20));
    options.AddPolicy("read", context => PerMinute(UserOrIp(context), 60));
    options.AddPolicy("auth", context => PerMinute(Ip(context), authLimit));
    options.AddPolicy("available", context => PerMinute(Ip(context), 30));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await Results.Problem(title: "Too many attempts. Try again in a moment", statusCode: 429)
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

// Opret databasen hvis den ikke findes
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<GameDb>().Database.EnsureCreated();
}

// Aldrig stacktrace eller exception-tekst til brugeren
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is BadHttpRequestException bad)
    {
        context.Response.StatusCode = bad.StatusCode;
        await Results.Problem(title: "The request is invalid or too large", statusCode: bad.StatusCode)
            .ExecuteAsync(context);
        return;
    }
    context.Response.StatusCode = 500;
    await Results.Problem(title: "Something went wrong on the server", statusCode: 500).ExecuteAsync(context);
}));

// Kun HTTPS. Uden for udvikling siger HSTS også til klienten, at den aldrig må bruge http. CORS er ikke slået til
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ---------- Login ----------

// Log ind med brugernavn og kodeord. Svaret er Identity's bearer-token (accessToken, expiresIn, refreshToken).
// Efter 5 forkerte kodeord er kontoen låst i 5 minutter. Fejl giver altid samme 401, så man ikke kan se, om navnet findes
app.MapPost("/api/auth/login", async (LoginRequest body, SignInManager<IdentityUser> signInManager) =>
{
    signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
    var result = await signInManager.PasswordSignInAsync((body.Username ?? "").Trim(), body.Password ?? "", false, lockoutOnFailure: true);
    if (!result.Succeeded)
        return Results.Problem(title: WrongLogin, statusCode: 401);
    return Results.Empty;   // SignInManager har allerede skrevet tokenet i svaret
})
.RequireRateLimiting("auth");

// Nyt adgangstoken fra et refresh-token (samme som Identity's egen /refresh). Afvises hvis det er udløbet,
// eller hvis brugeren er slettet eller har skiftet kodeord siden
app.MapPost("/api/auth/refresh", async (RefreshRequest body, SignInManager<IdentityUser> signInManager, IOptionsMonitor<BearerTokenOptions> bearerOptions) =>
{
    var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
    var ticket = protector.Unprotect(body.RefreshToken ?? "");
    if (ticket?.Properties.ExpiresUtc is not { } expires || DateTimeOffset.UtcNow >= expires)
        return Results.Problem(title: "Sign in again", statusCode: 401);
    var user = await signInManager.ValidateSecurityStampAsync(ticket.Principal);
    if (user == null)
        return Results.Problem(title: "Sign in again", statusCode: 401);

    var principal = await signInManager.CreateUserPrincipalAsync(user);
    return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
})
.RequireRateLimiting("auth");

// ---------- Konto ----------

// Opret konto med brugernavn og kodeord. Navnet skal være ledigt (store/små bogstaver er ens)
app.MapPost("/api/account", async (CreateAccountRequest body, UserManager<IdentityUser> userManager) =>
{
    var username = (body.Username ?? "").Trim();
    if (!Nickname.IsValid(username))
        return Results.Problem(title: "The username must be 3-16 letters, digits or underscores", statusCode: 400);
    if (await userManager.FindByNameAsync(username) != null)
        return Results.Problem(title: "That username is taken", statusCode: 409);

    try
    {
        var result = await userManager.CreateAsync(new IdentityUser { UserName = username }, body.Password ?? "");
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
            return Results.Problem(title: "That username is taken", statusCode: 409);
        if (!result.Succeeded)
        {
            // Samme form som Identity's egne valideringsfejl
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            return Results.ValidationProblem(errors);
        }
    }
    catch (DbUpdateException)
    {
        // To der vælger samme navn samtidig: det unikke indeks i databasen tillader kun den ene
        return Results.Problem(title: "That username is taken", statusCode: 409);
    }
    return Results.NoContent();
})
.RequireRateLimiting("auth");

// Er brugernavnet ledigt? Bruges mens man skriver i appen. Åben, men med rate limit pr. IP
app.MapGet("/api/account/available", async (string? username, UserManager<IdentityUser> userManager) =>
{
    var name = (username ?? "").Trim();
    if (!Nickname.IsValid(name))
        return Results.Problem(title: "The username must be 3-16 letters, digits or underscores", statusCode: 400);
    return Results.Ok(new AvailableResponse(await userManager.FindByNameAsync(name) == null));
})
.RequireRateLimiting("available");

app.MapGet("/api/account", async (ClaimsPrincipal principal, UserManager<IdentityUser> userManager) =>
{
    var user = await userManager.FindByIdAsync(UserId(principal));
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);
    return Results.Ok(new AccountResponse(user.UserName ?? ""));
})
.RequireAuthorization()
.RequireRateLimiting("read");

// Skift brugernavn (også navnet på top-listen). Tokens gælder stadig, så man ikke bliver logget ud
app.MapPut("/api/account/username", async (UsernameRequest body, ClaimsPrincipal principal, UserManager<IdentityUser> userManager) =>
{
    var username = (body.Username ?? "").Trim();
    if (!Nickname.IsValid(username))
        return Results.Problem(title: "The username must be 3-16 letters, digits or underscores", statusCode: 400);

    var user = await userManager.FindByIdAsync(UserId(principal));
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);
    var other = await userManager.FindByNameAsync(username);
    if (other != null && other.Id != user.Id)
        return Results.Problem(title: "That username is taken", statusCode: 409);

    user.UserName = username;
    try
    {
        var result = await userManager.UpdateAsync(user);   // sætter også det normaliserede navn
        if (!result.Succeeded)
            return Results.Problem(title: "That username is taken", statusCode: 409);
    }
    catch (DbUpdateException)
    {
        return Results.Problem(title: "That username is taken", statusCode: 409);
    }
    return Results.NoContent();
})
.RequireAuthorization()
.RequireRateLimiting("read");

// Slet min konto (retten til sletning). Tiderne og spøgelserne slettes med. Id kommer fra tokenet
app.MapDelete("/api/account", async (ClaimsPrincipal principal, UserManager<IdentityUser> userManager) =>
{
    var user = await userManager.FindByIdAsync(UserId(principal));
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);

    var result = await userManager.DeleteAsync(user);
    if (!result.Succeeded)
        return Results.Problem(title: "The account could not be deleted", statusCode: 500);
    return Results.NoContent();
})
.RequireAuthorization();

// ---------- Tider ----------

// Dagens labyrint: seed er datoen (UTC) som tal. Ens for alle
app.MapGet("/api/daily", () => Results.Ok(new { Seed = Course.DailySeed() }));

app.MapPost("/api/runs", async (RunRequest body, ClaimsPrincipal principal, GameDb db, UserManager<IdentityUser> userManager, ILogger<Program> log) =>
{
    var userId = UserId(principal);
    var user = await userManager.FindByIdAsync(userId);
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);

    if (!Enum.IsDefined(body.Difficulty) || body.Seed < 0)
        return Results.Problem(title: "Run could not be verified", statusCode: 400);

    // Spøgelset er base64 af float32. Alt galt her er også en tur, der ikke kan tjekkes
    byte[] bytes;
    try
    {
        bytes = Convert.FromBase64String(body.Ghost ?? "");
    }
    catch (FormatException)
    {
        return Results.Problem(title: "Run could not be verified", statusCode: 400);
    }
    if (bytes.Length > MaxGhostBytes || bytes.Length % 8 != 0)
        return Results.Problem(title: "Run could not be verified", statusCode: 400);

    // Serveren bygger selv labyrinten og afspiller spøgelset mod den
    var course = new Course(body.Difficulty, body.Seed);
    var why = RunChecker.Check(course, body.TimeMs, Ghost.FromBytes(bytes));
    if (why != null)
    {
        log.LogWarning("Run rejected for user {UserId}: {Why}", userId, why);
        return Results.Problem(title: "Run could not be verified", statusCode: 400);
    }

    // Kun den bedste tid pr. bruger og labyrint gemmes
    var run = await db.Runs.FirstOrDefaultAsync(r => r.UserId == userId && r.Difficulty == body.Difficulty && r.Seed == body.Seed);
    bool isBest = run == null || body.TimeMs < run.TimeMs;
    if (run == null)
    {
        run = new RunEntity { UserId = userId, Difficulty = body.Difficulty, Seed = body.Seed };
        db.Runs.Add(run);
    }
    if (isBest)
    {
        run.TimeMs = body.TimeMs;
        run.Ghost = bytes;
        run.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    int rank = await db.Runs.CountAsync(r => r.Difficulty == body.Difficulty && r.Seed == body.Seed && r.TimeMs < run.TimeMs) + 1;
    return Results.Ok(new RunResponse(run.Id, run.TimeMs, isBest, rank));
})
.RequireAuthorization()
.RequireRateLimiting("runs")
.WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes));

// Top-liste for en labyrint. Åben for alle; er man logget ind, er ens egen linje markeret
app.MapGet("/api/leaderboard/{difficulty}/{seed:int}", async (Difficulty difficulty, int seed, int? top, ClaimsPrincipal principal, GameDb db) =>
{
    int count = Math.Clamp(top ?? 10, 1, 100);
    var myId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

    var rows = await db.Runs
        .Where(r => r.Difficulty == difficulty && r.Seed == seed)
        .OrderBy(r => r.TimeMs).ThenBy(r => r.Id)
        .Take(count)
        .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new { r.Id, r.UserId, u.UserName, r.TimeMs })
        .ToListAsync();

    var list = new List<LeaderboardRow>();
    foreach (var row in rows)
        list.Add(new LeaderboardRow(list.Count + 1, row.Id, row.UserName ?? "?", row.TimeMs, row.UserId == myId));
    return Results.Ok(list);
})
.RequireRateLimiting("read");

// Spøgelset fra en tur, så man kan ræse mod det. Kun for de 10 bedste på labyrinten og ens egne
app.MapGet("/api/runs/{id:int}/ghost", async (int id, ClaimsPrincipal principal, GameDb db) =>
{
    var userId = UserId(principal);
    var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == id);
    if (run == null)
        return Results.Problem(title: "The run does not exist", statusCode: 404);

    int better = await db.Runs.CountAsync(r =>
        r.Difficulty == run.Difficulty && r.Seed == run.Seed && (r.TimeMs < run.TimeMs || (r.TimeMs == run.TimeMs && r.Id < run.Id)));
    if (run.UserId != userId && better >= TopGhosts)
        return Results.Problem(title: "The run does not exist", statusCode: 404);

    var username = await db.Users.Where(u => u.Id == run.UserId).Select(u => u.UserName).FirstAsync();
    return Results.Ok(new GhostResponse(username ?? "?", run.TimeMs, Convert.ToBase64String(run.Ghost)));
})
.RequireAuthorization()
.RequireRateLimiting("read");

app.Run();

// Brugerens id kommer altid fra tokenet, aldrig fra forespørgslen
static string UserId(ClaimsPrincipal principal)
{
    return principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

static string Ip(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// Bruger-id fra tokenet, ellers IP-adressen (til rate limit)
static string UserOrIp(HttpContext context)
{
    return context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Ip(context);
}

// Højst "limit" forespørgsler i minuttet for hver nøgle (bruger eller IP)
static RateLimitPartition<string> PerMinute(string key, int limit)
{
    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = limit,
        Window = TimeSpan.FromMinutes(1)
    });
}

// Gør Program synlig for endpoint-testene
public partial class Program { }
