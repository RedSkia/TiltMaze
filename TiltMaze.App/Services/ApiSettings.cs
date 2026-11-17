using System.Net.Http.Json;
using System.Text.Json;

namespace TiltMaze.App.Services;

public static class ApiSettings
{
    // Publishable key er offentlig og sikker, fordi RLS og RPC-rettigheder i databasen styrer hvad man må.
    // Den sendes kun i headeren apikey (den er ingen JWT). Supabase-adgangstokenet (PAT) bruges aldrig af appen,
    // kun af CLI'en til deploy
    public const string SupabaseUrl = "https://uieqaiauayzibatdngbe.supabase.co";
    public const string PublishableKey = "sb_publishable_suS99ziw0TxH6QGfrajPPw_fYi2WzWr";

    // Google sender login tilbage til appen på denne adresse (står også i Supabase og i Android-manifestet)
    public const string GoogleCallback = "com.redskia.tiltmaze://login-callback";
    // På Windows kommer koden til en lytter på pc'en i stedet (adressen skal også stå i Supabase)
    public const string GoogleWindowsCallback = "http://127.0.0.1:53682/";

    // E-mail til Supabase ud fra brugernavnet (brugeren ser aldrig en e-mail)
    public static string EmailOf(string username)
    {
        return username.Trim().ToLowerInvariant() + "@tiltmaze.invalid";
    }

    // HttpClient til Supabase. Bruges af både Auth og Api. Bearer sættes kun på kald med brugerens token
    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(SupabaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        http.DefaultRequestHeaders.Add("apikey", PublishableKey);
        return http;
    }

    // Sender til serveren. Returnerer null, hvis der ikke er forbindelse
    public static async Task<HttpResponseMessage?> TrySend(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    // Læser JSON fra svaret. Er svaret ikke JSON (fx fra en proxy), giver den null i stedet for en fejl
    public static async Task<T?> ReadJson<T>(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
