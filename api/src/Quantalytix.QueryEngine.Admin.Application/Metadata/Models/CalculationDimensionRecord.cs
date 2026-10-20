namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_dimension</c> row: a dimension shared by a report version's datasets, which runs break out and filter by.</summary>
public sealed record CalculationDimensionRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The owning report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>Key, unique among the version's calculation dimensions.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>calculation_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The canonical dimension of a <c>canonical</c> calculation dimension; queries use the version their assumption set pins.</summary>
    public long? DimensionId { get; init; }

    /// <summary>The shared raw value type of a <c>scalar</c> calculation dimension.</summary>
    public string? ValueDataType { get; init; }

    /// <summary>A <c>grouping_mode</c> value of a <c>canonical</c> calculation dimension: how its routes group members.</summary>
    public string? GroupingMode { get; init; }

    /// <summary>The key of the level to group at when the grouping mode is <c>level</c>.</summary>
    public string? GroupingLevelKey { get; init; }

    /// <summary>The key of the ancestor member to group under when the grouping mode is <c>ancestor</c>.</summary>
    public string? GroupingAncestorMemberKey { get; init; }

    /// <summary>The closure depth to group at when the grouping mode is <c>depth</c>.</summary>
    public int? GroupingDepth { get; init; }

    /// <summary>Whether runs may break out by it.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether report filters may compare it.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
