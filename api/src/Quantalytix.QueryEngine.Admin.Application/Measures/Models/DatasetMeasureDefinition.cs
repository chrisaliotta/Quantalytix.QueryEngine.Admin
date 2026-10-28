namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>The contract's <c>DatasetMeasureDefinition</c>: the writable content of a measure (<c>qe_measure</c>), its components and time policy.</summary>
public abstract record DatasetMeasureDefinition
{
    /// <summary>The measure key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>A <c>measure_kind</c> value.</summary>
    public required string MeasureKind { get; init; }

    /// <summary>An <c>aggregation_function</c> value.</summary>
    public required string? AggregationFunction { get; init; }

    /// <summary>The formula of a formula measure, composing other measures (<c>[m.key]</c>).</summary>
    public required string? ExpressionText { get; init; }

    /// <summary>The implementation of a specialized measure.</summary>
    public required string? ImplementationKey { get; init; }

    /// <summary>The implementation version of a specialized measure.</summary>
    public required string? ImplementationVersion { get; init; }

    /// <summary>The implementation configuration as JSON text.</summary>
    public required string? ConfigurationJson { get; init; }

    /// <summary>The fields the measure reads, one per role.</summary>
    public required IReadOnlyList<DatasetMeasureComponent> Components { get; init; }

    /// <summary>The row eligibility predicate.</summary>
    public required string? RowFilterExpression { get; init; }

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

    /// <summary>The output's decimal precision.</summary>
    public required int? OutputPrecision { get; init; }

    /// <summary>The output's decimal scale.</summary>
    public required int? OutputScale { get; init; }

    /// <summary>A <c>rounding_mode</c> value; null when the measure names none, which the engine reads as no rounding.</summary>
    public required string? RoundingMode { get; init; }

    /// <summary>The unit, currency or rate label.</summary>
    public required string? UnitKey { get; init; }

    /// <summary>The accumulator's decimal precision.</summary>
    public required int AccumulatorPrecision { get; init; }

    /// <summary>The accumulator's decimal scale.</summary>
    public required int AccumulatorScale { get; init; }

    /// <summary>The time policy; null for none.</summary>
    public required DatasetMeasureTimePolicy? TimePolicy { get; init; }
}
