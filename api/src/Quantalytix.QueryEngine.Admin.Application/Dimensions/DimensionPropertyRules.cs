using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a system dimension member's properties against its profile's property schema (OI-18): required fields have a value and
/// every value has its field's type. Keys outside the schema are kept as they are. User dimensions keep free key/value.
/// </summary>
public static class DimensionPropertyRules
{
    /// <summary>The problems of a member's properties.</summary>
    /// <param name="profile">The dimension's profile.</param>
    /// <param name="properties">The member's properties.</param>
    /// <returns>Each problem with the property key it concerns; empty when the properties fit.</returns>
    public static IEnumerable<(string Key, string Message)> Problems(DimensionProfile profile, JsonObject properties)
    {
        foreach (var field in profile.MemberPropertySchema)
        {
            var value = properties[field.Key];
            if (value is null || value.GetValueKind() == JsonValueKind.Null || (value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Length == 0))
            {
                if (field.Required == true) yield return (field.Key, $"{field.Label} is required.");
                continue;
            }

            if (TypeProblem(field, value) is { } problem) yield return (field.Key, problem);
        }
    }

    /// <summary>What is wrong with a present value for its field's type, or null when it fits.</summary>
    private static string? TypeProblem(DimensionPropertyField field, JsonNode value)
    {
        var kind = value.GetValueKind();
        var text = kind == JsonValueKind.String ? value.GetValue<string>() : null;
        return field.Type switch
        {
            DimensionPropertyTypes.Number => kind == JsonValueKind.Number ? null : $"{field.Label} must be a number.",
            DimensionPropertyTypes.Boolean => kind is JsonValueKind.True or JsonValueKind.False ? null : $"{field.Label} must be true or false.",
            DimensionPropertyTypes.Date => text is not null && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? null
                : $"{field.Label} must be a date 'yyyy-MM-dd'.",
            DimensionPropertyTypes.Url or DimensionPropertyTypes.Image => text is not null && Uri.TryCreate(text, UriKind.Absolute, out _) ? null : $"{field.Label} must be an absolute URL.",
            DimensionPropertyTypes.Select => text is not null && field.Options!.Contains(text) ? null : $"{field.Label} must be one of {string.Join(", ", field.Options!)}.",
            DimensionPropertyTypes.Geo => text is not null || kind == JsonValueKind.Object ? null : $"{field.Label} must be text or a location object.",
            _ => text is null ? $"{field.Label} must be text." : field.Format == DimensionPropertyTypes.GeoJsonFormat && !IsJsonObject(text) ? $"{field.Label} must be a GeoJSON object." : null
        };
    }

    /// <summary>Whether text parses as a JSON object.</summary>
    private static bool IsJsonObject(string text)
    {
        try
        {
            return JsonNode.Parse(text) is JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
