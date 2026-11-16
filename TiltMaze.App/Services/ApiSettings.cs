using System.Net.Http.Headers;

namespace TiltMaze.App.Services;

public static class ApiSettings
{
    // Anon-nøglen er offentlig og sikker, fordi RLS i databasen styrer hvad man må
    public const string SupabaseUrl = "https://YOUR-PROJECT.supabase.co";
    public const string AnonKey = "YOUR-ANON-KEY";

    // E-mail til Supabase ud fra brugernavnet (brugeren ser aldrig en e-mail)
    public static string EmailOf(string username)
    {
        return username.Trim().ToLowerInvariant() + "@tiltmaze.invalid";
    }

    // HttpClient til Supabase. Bruges af både Auth og Api. Uden login sendes anon-nøglen som token
    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(SupabaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        http.DefaultRequestHeaders.Add("apikey", AnonKey);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AnonKey);
        return http;
    }
}
