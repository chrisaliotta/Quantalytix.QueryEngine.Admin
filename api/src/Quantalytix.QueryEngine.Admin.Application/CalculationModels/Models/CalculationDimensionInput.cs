namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A calculation dimension of a draft model version, as its create and edit endpoints take it.</summary>
public sealed record CalculationDimensionInput
{
    /// <summary>Key, unique among the version's calculation dimensions.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>calculation_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The canonical dimension of a <c>canonical</c> calculation dimension; null for <c>scalar</c>.</summary>
    public long? DimensionId { get; init; }

    /// <summary>The shared raw value type of a <c>scalar</c> calculation dimension, a <c>data_type</c> value; null for <c>canonical</c>.</summary>
    public string? ValueDataType { get; init; }

    /// <summary>Whether reports may break out by it.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether report filters may compare it.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
