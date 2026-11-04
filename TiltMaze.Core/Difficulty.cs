namespace TiltMaze.Core;

public enum Difficulty { Easy, Normal, Hard, Expert }

// Hvad en sværhedsgrad betyder: størrelse, rum, løkker (braiding), huller og mønter.
// Små huller kan kuglen rulle forbi langs siden af gangen, store huller fylder hele gangen.
public record DifficultySettings(int Width, int Height, int Rooms, double Braid, int SmallHoles, int BigHoles, double SmallMin, double SmallMax, int Coins)
{
    public static DifficultySettings For(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Easy => new DifficultySettings(5, 7, 1, 0.7, 2, 1, 0.13, 0.15, 3),
            Difficulty.Normal => new DifficultySettings(7, 10, 2, 0.5, 4, 2, 0.13, 0.16, 5),
            Difficulty.Hard => new DifficultySettings(9, 13, 3, 0.5, 8, 5, 0.14, 0.17, 7),
            _ => new DifficultySettings(11, 16, 4, 0.5, 12, 8, 0.15, 0.175, 9),
        };
    }
}
