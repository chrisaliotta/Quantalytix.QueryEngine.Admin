namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_calculation_dimension</c> row: a dimension shared by a report's datasets, which runs break out and filter by.
/// It reads a dimension role, grouped as it says unless a report request names another grouping; a source reaches it when its
/// dataset binds the role.
/// </summary>
public sealed record CalculationDimensionRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>Key, unique among the report's calculation dimensions.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The role every source reads; queries use the version of its dimension their assumption set pins.</summary>
    public required long RoleId { get; init; }

    /// <summary>A <c>grouping_mode</c> value: how the role's members group by default.</summary>
    public required string GroupingMode { get; init; }

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

    /// <summary>
    /// Whether its members are plan versions, such as budgets and forecasts: a report has at most one such dimension, which is
    /// neither groupable nor filterable, and a run names one of its members whenever one of its sources reaches it.
    /// </summary>
    public required bool IsScenario { get; init; }
}
