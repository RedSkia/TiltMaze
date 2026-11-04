using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TiltMaze.Core;

namespace TiltMaze.Api;

// Konto: opret, ledigt navn, hent, skift brugernavn og slet.
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
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
        var user = await userManager.FindByIdAsync(Users.UserId(principal));
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

        var user = await userManager.FindByIdAsync(Users.UserId(principal));
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
        var user = await userManager.FindByIdAsync(Users.UserId(principal));
        if (user == null)
            return Results.Problem(title: "The account does not exist", statusCode: 404);

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return Results.Problem(title: "The account could not be deleted", statusCode: 500);
        return Results.NoContent();
    })
    .RequireAuthorization();
    }
}
