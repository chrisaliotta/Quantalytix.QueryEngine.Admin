using Quantalytix.QueryEngine.Admin.Application.Dimensions;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A calculation dimension of a report, as its create and edit endpoints take it.</summary>
public sealed record CalculationDimensionInput : IGroupingInput
{
    /// <summary>Key, unique among the report's calculation dimensions.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The role whose binding each source's dataset classifies rows by; its template is the dimension whose members are grouped.</summary>
    public required long RoleId { get; init; }

    /// <summary>A <c>grouping_mode</c> value: how runs group the role's members unless a report request names another grouping.</summary>
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
    /// Whether its members are plan versions, such as budgets and forecasts, which runs never add together: a report has at most
    /// one such dimension, and it is neither groupable nor filterable.
    /// </summary>
    public required bool IsScenario { get; init; }
}
