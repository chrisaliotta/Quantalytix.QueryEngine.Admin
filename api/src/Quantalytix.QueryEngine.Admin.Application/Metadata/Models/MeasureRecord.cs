namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_measure</c> row: a backend-defined measure that a request's column measure resolves.</summary>
public sealed record MeasureRecord
{
    /// <summary>Primary key.</summary>
    public required long MeasureId { get; init; }

    /// <summary>The owning dataset version.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>Request-facing backend measure ID.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>measure_kind</c> value.</summary>
    public required string MeasureKind { get; init; }

    /// <summary>An <c>aggregation_function</c> value for ordinary aggregate measures.</summary>
    public string? AggregationFunction { get; init; }

    /// <summary>Validated measure-level expression for <c>formula</c> measures.</summary>
    public string? ExpressionText { get; init; }

    /// <summary>Optional validated source-row eligibility predicate.</summary>
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

    /// <summary>Decimal precision for displaying the output.</summary>
    public int? OutputPrecision { get; init; }

    /// <summary>Decimal scale for displaying the output.</summary>
    public int? OutputScale { get; init; }

    /// <summary>Internal decimal accumulator precision.</summary>
    public required int AccumulatorPrecision { get; init; }

    /// <summary>Internal decimal accumulator scale.</summary>
    public required int AccumulatorScale { get; init; }

    /// <summary>A <c>rounding_mode</c> value, or null when the measure names none.</summary>
    public string? RoundingMode { get; init; }

    /// <summary>Optional unit, currency or rate label.</summary>
    public string? UnitKey { get; init; }

    /// <summary>Allowlisted implementation identity for <c>specialized</c> measures.</summary>
    public string? ImplementationKey { get; init; }

    /// <summary>Version of the specialized implementation.</summary>
    public string? ImplementationVersion { get; init; }

    /// <summary>Implementation-specific configuration as raw JSON text (a JSON column).</summary>
    public string? ConfigurationJson { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
