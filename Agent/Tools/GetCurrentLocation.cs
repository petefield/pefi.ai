using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Tools;

public sealed class GetCurrentLocationTool : IAgentTool
{
    public string Name => "get_current_location";
    public string Description => "Gets the current location. Returns a JSON string with location details including country, region, city, zip, latitude, and longitude.";
    public JsonElement ParametersSchema => ToolSchema.EmptyObject();

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var location = await GeoLocationService.GetLocationAsync();
        return ToolResult.Ok(location.ToString());
    }
}
