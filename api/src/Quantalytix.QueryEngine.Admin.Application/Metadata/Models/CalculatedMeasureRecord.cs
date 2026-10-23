namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculated_measure</c> row: a formula over a model's variables and other calculated measures.</summary>
public sealed record CalculatedMeasureRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculatedMeasureId { get; init; }

    /// <summary>The owning model.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>Key, unique among the model's variables and calculated measures.</summary>
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
