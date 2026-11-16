using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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
