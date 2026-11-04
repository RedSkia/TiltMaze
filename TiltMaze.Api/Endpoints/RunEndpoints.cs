using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiltMaze.Core;

namespace TiltMaze.Api;

// Tider: dagens labyrint, send en tur og hent et spøgelse.
public static class RunEndpoints
{
    const int MaxGhostBytes = RunChecker.MaxSamples * 8;   // 8 bytes pr. måling (x og y)
    const int MaxRequestBytes = MaxGhostBytes * 4 / 3 + 1024;   // base64 er en tredjedel større end bytes
    const int TopGhosts = 10;                      // spøgelset kan hentes for de 10 bedste (og ens egne)

    public static void MapRunEndpoints(this WebApplication app)
    {
    // Dagens labyrint: seed er datoen (UTC) som tal. Ens for alle
    app.MapGet("/api/daily", () => Results.Ok(new { Seed = Course.DailySeed() }));

    app.MapPost("/api/runs", async (RunRequest body, ClaimsPrincipal principal, GameDb db, UserManager<IdentityUser> userManager, ILogger<Program> log) =>
    {
        var userId = Users.UserId(principal);
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
    // Spøgelset fra en tur, så man kan ræse mod det. Kun for de 10 bedste på labyrinten og ens egne
    app.MapGet("/api/runs/{id:int}/ghost", async (int id, ClaimsPrincipal principal, GameDb db) =>
    {
        var userId = Users.UserId(principal);
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
    }
}
