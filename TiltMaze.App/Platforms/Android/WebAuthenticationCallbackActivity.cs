using Android.App;
using Android.Content;
using Android.Content.PM;

namespace TiltMaze.App;

// Modtager Google-login fra browseren (com.redskia.tiltmaze://login-callback) og giver det til WebAuthenticator.
// Exported = true, fordi browseren skal kunne åbne den; den gør intet andet end at aflevere adressen
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.redskia.tiltmaze", DataHost = "login-callback")]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
}
