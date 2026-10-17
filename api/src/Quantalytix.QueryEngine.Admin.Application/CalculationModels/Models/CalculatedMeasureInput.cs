namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A calculated measure of a draft model version, as its create and edit endpoints take it.</summary>
public sealed record CalculatedMeasureInput
{
    /// <summary>Key, unique among the version's variables and calculated measures.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Expression over <c>[m.KEY]</c> references and the <c>@periodDays</c> and <c>@yearDays</c> parameters.</summary>
    public required string ExpressionText { get; init; }

    /// <summary>A <c>measure_output_data_type</c> value.</summary>
    public required string OutputDataType { get; init; }

    /// <summary>Decimal precision of the output for display.</summary>
    public int? OutputPrecision { get; init; }

    /// <summary>Decimal scale of the output for display.</summary>
    public int? OutputScale { get; init; }

    /// <summary>A <c>rounding_mode</c> value, or null for half to even.</summary>
    public string? RoundingMode { get; init; }

    /// <summary>Optional unit, such as a currency code.</summary>
    public string? UnitKey { get; init; }

    /// <summary>Presentation hint such as <c>number</c>, <c>percent</c> or <c>currency</c>.</summary>
    public required string DisplayFormat { get; init; }
}
