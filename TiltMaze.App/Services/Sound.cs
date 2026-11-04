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

    // Lyd til/fra, gemmes på enheden (standard: til). JavaScript får det også at vide, til klik på knapper
    public bool Enabled
    {
        get => Preferences.Default.Get("sound", true);
        set
        {
            Preferences.Default.Set("sound", value);
            Sync();
        }
    }

    public void Coin() => Play("coin");
    public void Fall() => Play("fall");
    public void Countdown() => Play("countdown");
    public void Go() => Play("go");
    public void Finish() => Play("finish");
    public void Record() => Play("record");
    public void Buy() => Play("buy");
    public void Equip() => Play("equip");
    public void Pause() => Play("pause");
    public void Resume() => Play("resume");
    public void Bump(double volume) => Play("bump", volume);

    // Fortæller JavaScript, om lyden er slået til (kaldes ved start og når kontakten skiftes)
    public void Sync() => Call("setEnabled", Enabled);

    private void Play(string name, double volume = 1)
    {
        if (Enabled)
            Call(name, volume);
    }

    // Fire and forget: spillet venter ikke på lyden, og en fejl i lyden må ikke stoppe spillet
    private async void Call(string name, object argument)
    {
        try
        {
            await js.InvokeVoidAsync("tiltMaze.sound." + name, argument);
        }
        catch
        {
            // ingen lyd, fx mens siden lukkes
        }
    }
}
