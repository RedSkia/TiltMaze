using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TiltMaze.Api;

// Login og refresh af tokens.
public static class AuthEndpoints
{
    const string WrongLogin = "Wrong username or password";   // samme tekst for ukendt bruger, forkert kodeord og låst konto

    public static void MapAuthEndpoints(this WebApplication app)
    {
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
    }
}
