using System.Text.RegularExpressions;

namespace TiltMaze.Core;

// Reglen for kaldenavne, ens i appen og på serveren: 3-16 bogstaver (A-Z), tal eller _
public static class Nickname
{
    public const string Pattern = "^[A-Za-z0-9_]{3,16}$";

    public static bool IsValid(string nickname)
    {
        return Regex.IsMatch(nickname, Pattern);
    }
}
