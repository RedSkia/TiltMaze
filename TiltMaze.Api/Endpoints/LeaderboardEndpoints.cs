using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TiltMaze.Core;

namespace TiltMaze.Api;

// Top-liste for en labyrint.
public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this WebApplication app)
    {
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
    }
}
