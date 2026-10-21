using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads the calculation dimensions a report's default filter and default pivot name.</summary>
public static class ReportDefaults
{
    /// <summary>The pivot's row dimensions, then its column dimensions.</summary>
    /// <param name="pivotJson">A well-formed default pivot, or null.</param>
    /// <returns>The dimension keys; none for no pivot.</returns>
    public static IReadOnlyList<string> PivotDimensions(string? pivotJson)
    {
        if (pivotJson is null) return [];
        var pivot = JsonSerializer.Deserialize<JsonElement>(pivotJson);
        return [.. new[] { "rowDimensions", "columnDimensions" }
            .Where(name => pivot.TryGetProperty(name, out _))
            .SelectMany(name => pivot.GetProperty(name).EnumerateArray().Select(key => key.GetString()!))];
    }

    /// <summary>The calculation dimensions a well-formed default filter compares.</summary>
    /// <param name="filterJson">A well-formed default filter, or null.</param>
    /// <returns>The distinct dimension keys; none for no filter.</returns>
    public static IReadOnlyList<string> FilterDimensions(string? filterJson)
    {
        if (filterJson is null) return [];
        return [.. FilterNodes.Inspect(filterJson).Operands.Select(operand => operand.Id).Distinct(StringComparer.Ordinal)];
    }
}
