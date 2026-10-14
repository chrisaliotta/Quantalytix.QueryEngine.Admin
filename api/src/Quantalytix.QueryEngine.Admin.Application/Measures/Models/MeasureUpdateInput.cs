namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// The editable attributes of a measure with its components and optional time policy. <see cref="MeasureInput"/> adds the
/// key, which is fixed at creation. The formula dependencies are derived from the expression, never sent.
/// </summary>
public record MeasureUpdateInput
{
    /// <summary>Human-readable measure name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// A <c>measure_kind</c> value: <c>aggregate</c>, <c>weighted_sum</c>, <c>weighted_average</c>,
    /// <c>period_average</c>, <c>formula</c> or <c>specialized</c>.
    /// </summary>
    public required string MeasureKind { get; init; }

    /// <summary>An <c>aggregation_function</c> value; required for <c>aggregate</c> measures and optional for <c>specialized</c> ones.</summary>
    public string? AggregationFunction { get; init; }

    /// <summary>The formula of a <c>formula</c> measure over other measures of the version, such as <c>[m.interest] / [m.balance]</c>.</summary>
    public string? ExpressionText { get; init; }

    /// <summary>An optional row predicate over fields that limits the rows the measure reads, such as <c>[f.status] = 'open'</c>.</summary>
    public string? RowFilterExpression { get; init; }

    /// <summary>A <c>value_null_behavior</c> value.</summary>
    public required string ValueNullBehavior { get; init; }

    /// <summary>A <c>negative_weight_behavior</c> value.</summary>
    public required string NegativeWeightBehavior { get; init; }

    /// <summary>An <c>empty_set_behavior</c> value.</summary>
    public required string EmptySetBehavior { get; init; }

    /// <summary>A <c>bridge_factor_behavior</c> value.</summary>
    public required string BridgeFactorBehavior { get; init; }

    /// <summary>A <c>bridge_allocation_behavior</c> value.</summary>
    public required string BridgeAllocationBehavior { get; init; }

    /// <summary>A <c>total_behavior</c> value.</summary>
    public required string TotalBehavior { get; init; }

    /// <summary>A <c>measure_output_data_type</c> value.</summary>
    public required string OutputDataType { get; init; }

    /// <summary>Display precision (1-38); required for <c>decimal</c> outputs and null otherwise.</summary>
    public int? OutputPrecision { get; init; }

    /// <summary>Display scale (0 up to the precision); required for <c>decimal</c> outputs and null otherwise.</summary>
    public int? OutputScale { get; init; }

    /// <summary>Precision (1-38) of the decimal the engine accumulates products in; null only while a form leaves it empty, and rejected.</summary>
    public required int? AccumulatorPrecision { get; init; }

    /// <summary>Scale (0 up to the accumulator precision) of the decimal the engine accumulates products in; null is rejected.</summary>
    public required int? AccumulatorScale { get; init; }

    /// <summary>A <c>rounding_mode</c> value, or null when the client should not round.</summary>
    public string? RoundingMode { get; init; }

    /// <summary>Optional unit, currency or rate label, such as <c>USD</c> or <c>percent</c>.</summary>
    public string? UnitKey { get; init; }

    /// <summary>The allowlisted implementation of a <c>specialized</c> measure; null for every other kind.</summary>
    public string? ImplementationKey { get; init; }

    /// <summary>The implementation version of a <c>specialized</c> measure; null for every other kind.</summary>
    public string? ImplementationVersion { get; init; }

    /// <summary>The implementation configuration of a <c>specialized</c> measure, as JSON text; null for every other kind.</summary>
    public string? ConfigurationJson { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The fields the measure reads, each in its own role; empty for formulas.</summary>
    public required IReadOnlyList<MeasureComponentInput> Components { get; init; }

    /// <summary>The temporal policy; required for <c>period_average</c> measures and optional for every other kind.</summary>
    public MeasureTimePolicyInput? TimePolicy { get; init; }
}
