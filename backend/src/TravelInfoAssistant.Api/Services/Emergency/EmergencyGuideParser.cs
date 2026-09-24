using System.Text.Json;

namespace TravelInfoAssistant.Api.Services.Emergency;

public static class EmergencyGuideParser
{
    public static IReadOnlyList<string> ParseSteps(string stepsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(stepsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
