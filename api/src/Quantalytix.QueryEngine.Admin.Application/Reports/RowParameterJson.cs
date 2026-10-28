using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Row parameter values as the row tree carries them: a JSON number for a number parameter and a JSON string for a text parameter.</summary>
internal static class RowParameterJson
{
    /// <summary>A stored value as the editor returns it.</summary>
    /// <param name="value">A row's value of a parameter.</param>
    /// <returns>The value as a JSON number or string.</returns>
    public static JsonElement Of(ReportRowParameterRecord value)
    {
        return value.NumberValue is { } number ? JsonSerializer.SerializeToElement(number) : JsonSerializer.SerializeToElement(value.TextValue);
    }

    /// <summary>A sent value as a number or text.</summary>
    /// <param name="value">A value of the row tree.</param>
    /// <returns>The number of a JSON number that fits a decimal, or the text of a JSON string; both null for anything else.</returns>
    public static (decimal? Number, string? Text) Read(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => (number, null),
            JsonValueKind.String => (null, value.GetString()),
            _ => (null, null)
        };
    }

    /// <summary>The stored row of a validated value.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="rowKey">The row that sets the value.</param>
    /// <param name="parameterKey">The parameter.</param>
    /// <param name="value">A value the row validator let through, which is of the parameter's type.</param>
    /// <returns>The row to store.</returns>
    public static ReportRowParameterRecord Record(long reportId, string rowKey, string parameterKey, JsonElement value)
    {
        var (number, text) = Read(value);
        return new ReportRowParameterRecord { ReportId = reportId, RowKey = rowKey, ParameterKey = parameterKey, NumberValue = number, TextValue = text };
    }
}
