namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>How an input groups the members of a canonical dimension: by member, at a level, under an ancestor or at a depth.</summary>
public interface IGroupingInput
{
    /// <summary>A <c>grouping_mode</c> value.</summary>
    string? GroupingMode { get; }

    /// <summary>The key of the level to group at when the grouping mode is <c>level</c>.</summary>
    string? GroupingLevelKey { get; }

    /// <summary>The key of the ancestor member to group under when the grouping mode is <c>ancestor</c>.</summary>
    string? GroupingAncestorMemberKey { get; }

    /// <summary>The closure depth to group at when the grouping mode is <c>depth</c>.</summary>
    int? GroupingDepth { get; }
}
