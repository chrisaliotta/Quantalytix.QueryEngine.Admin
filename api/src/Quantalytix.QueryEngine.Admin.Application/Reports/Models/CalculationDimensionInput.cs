using Quantalytix.QueryEngine.Admin.Application.Dimensions;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A calculation dimension of an editable report version, as its create and edit endpoints take it.</summary>
public sealed record CalculationDimensionInput : IGroupingInput
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

    /// <summary>A <c>grouping_mode</c> value: how a <c>canonical</c> calculation dimension's routes group members; null for <c>scalar</c>.</summary>
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
