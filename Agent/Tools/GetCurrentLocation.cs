using System.Text.Json;
using LocalAgent.Agent.Core;
using LocalAgent.Agent.Tools.Location;

namespace LocalAgent.Agent.Tools;

public sealed class GetCurrentLocationTool : IAgentTool
{
    public string Name => "get_current_location";
    public string Description => "Gets the current location. Returns a JSON string with location details including country, region, city, zip, latitude, and longitude.";
    public JsonElement ParametersSchema => ToolSchema.EmptyObject();

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {

        var location = new GeoLocation
        {
            Country = "United Kingdom",
            City = "Dorking",
            Zip = "RH4 1NJ",
            Lat = 51.2324,
            Lon = -0.3333
        };

        return await Task.FromResult(ToolResult.Ok(location.ToString()));
    }
}
