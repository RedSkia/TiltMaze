namespace TiltMaze.App.Services;

public static class ApiSettings
{
    // Docker-API på PC'en. Android når den via "adb reverse" (sættes op automatisk ved build)
    public const string BaseUrl = "https://localhost:7065";

    // HttpClient til API'et. Bruges af både Auth og Api
    public static HttpClient CreateHttpClient()
    {
        return new HttpClient(CreateHandler())
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG && ANDROID
        // Kun under udvikling: Android stoler ikke på PC'ens udviklingscertifikat.
        // Vi godtager det kun for localhost, som via "adb reverse" peger på PC'ens Docker-API.
        handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
            errors == System.Net.Security.SslPolicyErrors.None || request.RequestUri?.IsLoopback == true;
#endif
        return handler;
    }
}
