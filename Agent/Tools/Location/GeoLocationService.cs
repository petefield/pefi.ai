using System.Text.Json;

namespace LocalAgent.Agent.Tools.Location;

public static class GeoLocationService
{
    private static async Task<string> GetCurrentIPAddress()
    {
        using var httpClient = new HttpClient();
        string ipAddress = await httpClient.GetStringAsync("https://api.ipify.org");
        return ipAddress;
    }

    public static async Task<GeoLocation> GetLocationAsync()
    {
        var ipAddress = await GetCurrentIPAddress();
        using var httpClient = new HttpClient();
        string url = $"http://ip-api.com/json/{ipAddress}";
        var response = await httpClient.GetStringAsync(url);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var location = JsonSerializer.Deserialize<GeoLocation>(response, options);
        if (location is null)
        {
            throw new InvalidOperationException("Failed to deserialize geolocation response.");
        }

        return location;
    }

}


