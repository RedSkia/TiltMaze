using Android.App;
using Android.Content.PM;
using Android.OS;

namespace TiltMaze.App;

// Exported = true: launcheren skal kunne starte appen udefra
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
