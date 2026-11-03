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
using TiltMaze.Api;
using TiltMaze.Core;

const int MaxGhostBytes = RunChecker.MaxSamples * 8;   // 8 bytes pr. måling (x og y)
const int MaxRequestBytes = MaxGhostBytes * 4 / 3 + 1024;   // base64 er en tredjedel større end bytes
const int TopGhosts = 10;                      // spøgelset kan hentes for de 10 bedste (og ens egne)

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<GameDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TiltMaze")));

// Brugere: Identity gemmer kodeord som hash og giver login/refresh med bearer-tokens
builder.Services.AddIdentityApiEndpoints<AppUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;   // derefter er kontoen låst i 5 minutter
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

// Rate limits: tider og læsning pr. bruger, login og oprettelse pr. IP-adresse
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("runs", context =>
        RateLimitPartition.GetFixedWindowLimiter(UserOrIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        }));
    options.AddPolicy("read", context =>
        RateLimitPartition.GetFixedWindowLimiter(UserOrIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1)
        }));
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        }));
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
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Ruter: POST /api/auth/register, /login, /refresh. Appen opretter konti med POST /api/account, som tager kaldenavn med.
// En konto lavet med /register har intet kaldenavn og kan ikke sende tider, før den får et
app.MapGroup("/api/auth").MapIdentityApi<AppUser>().RequireRateLimiting("auth");

// ---------- Konto ----------

// Opret konto med e-mail, kodeord og kaldenavn
app.MapPost("/api/account", async (CreateAccountRequest body, UserManager<AppUser> userManager) =>
{
    var email = (body.Email ?? "").Trim();
    var nickname = (body.Nickname ?? "").Trim();
    if (!Nickname.IsValid(nickname))
        return Results.Problem(title: "The nickname must be 3-16 letters, digits or underscores", statusCode: 400);
    if (await NicknameTaken(userManager, nickname, null))
        return Results.Problem(title: "That nickname is already taken", statusCode: 400);

    var user = new AppUser { UserName = email, Email = email, Nickname = nickname };
    try
    {
        var result = await userManager.CreateAsync(user, body.Password ?? "");
        if (!result.Succeeded)
        {
            // Samme form som Identity's egne valideringsfejl
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            return Results.ValidationProblem(errors);
        }
    }
    catch (DbUpdateException)
    {
        // To der vælger samme kaldenavn samtidig: databasen tillader kun den ene
        return Results.Problem(title: "That nickname is already taken", statusCode: 400);
    }
    return Results.NoContent();
})
.RequireRateLimiting("auth");

app.MapGet("/api/account", async (ClaimsPrincipal principal, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByIdAsync(UserId(principal));
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);
    return Results.Ok(new { user.Email, user.Nickname });
})
.RequireAuthorization()
.RequireRateLimiting("read");

app.MapPut("/api/account/nickname", async (NicknameRequest body, ClaimsPrincipal principal, UserManager<AppUser> userManager) =>
{
    var nickname = (body.Nickname ?? "").Trim();
    if (!Nickname.IsValid(nickname))
        return Results.Problem(title: "The nickname must be 3-16 letters, digits or underscores", statusCode: 400);

    var user = await userManager.FindByIdAsync(UserId(principal));
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);
    if (await NicknameTaken(userManager, nickname, user.Id))
        return Results.Problem(title: "That nickname is already taken", statusCode: 400);

    user.Nickname = nickname;
    try
    {
        await userManager.UpdateAsync(user);
    }
    catch (DbUpdateException)
    {
        return Results.Problem(title: "That nickname is already taken", statusCode: 400);
    }
    return Results.NoContent();
})
.RequireAuthorization()
.RequireRateLimiting("read");

// Slet min konto (retten til sletning). Tiderne og spøgelserne slettes med. Id kommer fra tokenet
app.MapDelete("/api/account", async (ClaimsPrincipal principal, UserManager<AppUser> userManager) =>
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

app.MapPost("/api/runs", async (RunRequest body, ClaimsPrincipal principal, GameDb db, UserManager<AppUser> userManager, ILogger<Program> log) =>
{
    var userId = UserId(principal);
    var user = await userManager.FindByIdAsync(userId);
    if (user == null)
        return Results.Problem(title: "The account does not exist", statusCode: 404);
    if (user.Nickname == null)
        return Results.Problem(title: "Choose a nickname first", statusCode: 400);

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
        .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new { r.Id, r.UserId, u.Nickname, r.TimeMs })
        .ToListAsync();

    var list = new List<LeaderboardRow>();
    foreach (var row in rows)
        list.Add(new LeaderboardRow(list.Count + 1, row.Id, row.Nickname ?? "?", row.TimeMs, row.UserId == myId));
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

    var nickname = await db.Users.Where(u => u.Id == run.UserId).Select(u => u.Nickname).FirstAsync();
    return Results.Ok(new GhostResponse(nickname ?? "?", run.TimeMs, Convert.ToBase64String(run.Ghost)));
})
.RequireAuthorization()
.RequireRateLimiting("read");

app.Run();

// Brugerens id kommer altid fra tokenet, aldrig fra forespørgslen
static string UserId(ClaimsPrincipal principal)
{
    return principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

// Bruger-id fra tokenet, ellers IP-adressen (til rate limit)
static string UserOrIp(HttpContext context)
{
    return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}

// Har en anden bruger allerede kaldenavnet? (store/små bogstaver er ens)
static async Task<bool> NicknameTaken(UserManager<AppUser> userManager, string nickname, string? exceptUserId)
{
    return await userManager.Users.AnyAsync(u => u.Nickname == nickname && u.Id != exceptUserId);
}

// Gør Program synlig for endpoint-testene
public partial class Program { }
