using System.Security.Claims;

namespace TiltMaze.Api;

// Hjælp til endpoints der kender brugeren
public static class Users
{
    // Brugerens id kommer altid fra tokenet, aldrig fra forespørgslen
    public static string UserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    }
}
