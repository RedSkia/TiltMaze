using Maui.Biometric;
using Microsoft.Extensions.Logging;
using TiltMaze.App.Services;

namespace TiltMaze.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBiometricAuthentication();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<GameDatabase>();
        builder.Services.AddSingleton<Biometrics>();
        builder.Services.AddSingleton<Auth>();
        builder.Services.AddSingleton<Wallet>();
        builder.Services.AddSingleton<Api>();
        builder.Services.AddSingleton<RunQueue>();
        builder.Services.AddSingleton<Scores>();
        builder.Services.AddSingleton<TiltSensor>();
        builder.Services.AddScoped<Sound>();   // Scoped: IJSRuntime hører til WebView'en, ikke hele appen

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Send rekorder, der ventede da appen sidst blev lukket (venter selv, hvis login er låst med fingeraftryk)
        _ = app.Services.GetRequiredService<RunQueue>().Start();
        return app;
    }
}
