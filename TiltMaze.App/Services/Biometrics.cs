using Maui.Biometric;

namespace TiltMaze.App.Services;

// Fingeraftryk / Windows Hello som hurtigt login.
// Tjekket låser kun det refresh-token op, der ligger på enheden (app-lås).
// Det er ikke kryptografisk bundet til tokenet. Appen får kun svaret ja/nej fra systemet,
// og ingen biometriske data bliver gemt eller sendt nogen steder.
public class Biometrics
{
    // Kun rigtig biometri, ikke PIN-kode
    private const Authenticator Kind = Authenticator.Biometric;

    // Valget "lås op med fingeraftryk" ligger i Preferences
    public bool Enabled
    {
        get => Preferences.Get("biometricLogin", false);
        set => Preferences.Set("biometricLogin", value);
    }

    // Har vi allerede spurgt brugeren én gang efter login?
    public bool Asked
    {
        get => Preferences.Get("biometricAsked", false);
        set => Preferences.Set("biometricAsked", value);
    }

    // Er der en sensor, og er der gemt et fingeraftryk / Windows Hello?
    public async Task<bool> IsAvailable()
    {
        try
        {
            var result = await BiometricAuthentication.Current.CheckAvailabilityAsync(Kind);
            return result.IsAvailable;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Viser systemets biometri-dialog. Returnerer false ved annuller eller fejl
    public async Task<bool> Ask(string reason)
    {
        try
        {
            var request = new AuthenticationRequest("TiltMaze", reason)
            {
                Authenticators = Kind,
                CancelTitle = "Use password"
            };
            var result = await BiometricAuthentication.Current.AuthenticateAsync(request);
            return result.IsSuccessful;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
