using System.Text.Json;
namespace LocalAgent.Agent.Tools.Location;

public class GeoLocation
{
    public string? Query { get; set; }  // IP
    public string? Country { get; set; }
    public string? RegionName { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }

    public override string ToString()
    {
        string jsonString = JsonSerializer.Serialize(this);
        return jsonString;
    }
}


