using System.Text.Json;

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
        return location;
    }

}


