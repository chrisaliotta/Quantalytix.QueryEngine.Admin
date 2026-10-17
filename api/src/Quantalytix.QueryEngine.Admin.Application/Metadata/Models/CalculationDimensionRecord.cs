namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_dimension</c> row: a dimension shared by a model version's datasets, which reports break out and filter by.</summary>
public sealed record CalculationDimensionRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The owning model version.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>Key, unique among the version's calculation dimensions.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>calculation_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The canonical dimension of a <c>canonical</c> calculation dimension; the release decides its version.</summary>
    public long? DimensionId { get; init; }

    /// <summary>The shared raw value type of a <c>scalar</c> calculation dimension.</summary>
    public string? ValueDataType { get; init; }

    /// <summary>Whether reports may break out by it.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether report filters may compare it.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
