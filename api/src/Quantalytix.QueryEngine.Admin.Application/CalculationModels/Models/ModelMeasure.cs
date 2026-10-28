namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>ModelMeasure</c>: a calculated measure (<c>qe_calculated_measure</c>), whose formula is <c>expression_text</c>.</summary>
public sealed record ModelMeasure
{
    /// <summary>Key, unique among the model's variables and calculated measures, fixed once saved.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Expression over <c>[m.KEY]</c> references, <c>@periodDays</c>, <c>@yearDays</c> and (1.2) the model's parameters.</summary>
    public required string Formula { get; init; }

    /// <summary>A <c>measure_output_data_type</c> value.</summary>
    public required string OutputDataType { get; init; }

    /// <summary>Decimal precision of the output; null unless the output is decimal.</summary>
    public required int? OutputPrecision { get; init; }

    /// <summary>Decimal scale of the output; null unless the output is decimal.</summary>
    public required int? OutputScale { get; init; }

    /// <summary>A <c>rounding_mode</c> value, or null for half to even.</summary>
    public required string? RoundingMode { get; init; }

    /// <summary>Optional unit, such as a currency code.</summary>
    public required string? UnitKey { get; init; }

    /// <summary>Presentation hint such as <c>number</c>, <c>percent</c> or <c>currency</c>.</summary>
    public required string DisplayFormat { get; init; }
}
