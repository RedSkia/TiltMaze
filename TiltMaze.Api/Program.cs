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

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapRunEndpoints();
app.MapLeaderboardEndpoints();

app.Run();

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
