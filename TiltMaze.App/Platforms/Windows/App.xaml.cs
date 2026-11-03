using Microsoft.UI.Xaml;

namespace TiltMaze.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        // WebView2 gemmer sine data i AppData i stedet for ved siden af .exe-filen.
        // Ligger projektet på Skrivebordet, blokerer Windows Defender (kontrolleret mappeadgang) ellers mappen,
        // og så kan WebView2 ikke starte.
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TiltMaze", "WebView2");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", folder);

        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
