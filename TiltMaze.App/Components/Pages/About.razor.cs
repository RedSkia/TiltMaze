namespace TiltMaze.App.Components.Pages;

public partial class About
{
    private readonly string version = AppInfo.Current.Version.ToString(2);

    // Åbner linket i den almindelige browser (et andet program)
    private async Task Open(string url)
    {
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.External);
        }
        catch (Exception)
        {
            var page = Application.Current!.Windows[0].Page!;
            await page.DisplayAlertAsync("Error", "Could not open the browser.", "OK");
        }
    }
}
