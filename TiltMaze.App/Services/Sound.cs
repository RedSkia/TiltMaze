using Microsoft.JSInterop;

namespace TiltMaze.App.Services;

// Lyde til spillet. De laves i game.js (tiltMaze.sound). Her tjekkes kun, om lyden er slået til.
public class Sound
{
    private readonly IJSRuntime js;

    public Sound(IJSRuntime js)
    {
        this.js = js;
    }

    // Lyd til/fra, gemmes på enheden (standard: til)
    public bool Enabled
    {
        get => Preferences.Default.Get("sound", true);
        set => Preferences.Default.Set("sound", value);
    }

    public void Coin() => Play("coin");
    public void Fall() => Play("fall");
    public void Countdown() => Play("countdown");
    public void Go() => Play("go");
    public void Finish() => Play("finish");
    public void Record() => Play("record");
    public void Buy() => Play("buy");

    // Fire and forget: spillet venter ikke på lyden, og en fejl i lyden må ikke stoppe spillet
    private async void Play(string name)
    {
        if (!Enabled)
            return;
        try
        {
            await js.InvokeVoidAsync("tiltMaze.sound." + name);
        }
        catch
        {
            // ingen lyd, fx mens siden lukkes
        }
    }
}
