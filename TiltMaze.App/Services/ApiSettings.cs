using System.Net.Http.Headers;

namespace TiltMaze.App.Services;

public static class ApiSettings
{
    // Anon-nøglen er offentlig og sikker, fordi RLS i databasen styrer hvad man må
    public const string SupabaseUrl = "https://uieqaiauayzibatdngbe.supabase.co";
    public const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InVpZXFhaWF1YXl6aWJhdGRuZ2JlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTQ4NDg0NjUsImV4cCI6MjExMDQyNDQ2NX0.Ltmg5-oLlbZYQKleTuguAa1VOcOTDOujYLsFzQEmJH8";

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
