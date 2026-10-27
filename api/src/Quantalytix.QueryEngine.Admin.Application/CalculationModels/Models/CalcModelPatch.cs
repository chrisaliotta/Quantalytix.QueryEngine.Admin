using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>CalcModelPatch</c>: the settings an <c>updateModel</c> operation changes; an absent setting is kept.</summary>
public sealed record CalcModelPatch
{
    /// <summary>The new name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    /// <summary>The new currency.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrencyCode { get; init; }

    /// <summary>The new calendar.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CalendarKey { get; init; }

    /// <summary>The new formula language.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FormulaLanguage { get; init; }

    /// <summary>The new attribution method.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributionMethod { get; init; }

    /// <summary>The new availability.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; init; }
}
