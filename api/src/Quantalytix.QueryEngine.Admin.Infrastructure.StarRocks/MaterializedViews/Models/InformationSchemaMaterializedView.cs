namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.MaterializedViews;

/// <summary>A row of <c>information_schema.materialized_views</c>, limited to the columns the refresh status needs.</summary>
internal sealed record InformationSchemaMaterializedView
{
    /// <summary>The view's name.</summary>
    public required string TableName { get; init; }

    /// <summary><c>true</c> while StarRocks maintains the view.</summary>
    public required string IsActive { get; init; }

    /// <summary>Why StarRocks stopped maintaining the view.</summary>
    public string? InactiveReason { get; init; }

    /// <summary>The state of the last refresh task, such as <c>PENDING</c>, <c>RUNNING</c>, <c>SUCCESS</c> or <c>FAILED</c>; empty before the first.</summary>
    public string? LastRefreshState { get; init; }

    /// <summary>When the last refresh task finished, as StarRocks writes it.</summary>
    public string? LastRefreshFinishedTime { get; init; }

    /// <summary>Why the last refresh task failed.</summary>
    public string? LastRefreshErrorMessage { get; init; }
}
