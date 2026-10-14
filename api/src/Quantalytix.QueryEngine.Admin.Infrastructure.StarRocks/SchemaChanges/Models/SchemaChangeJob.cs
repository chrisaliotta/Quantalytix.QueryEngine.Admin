namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

/// <summary>A row of <c>SHOW ALTER TABLE COLUMN</c>, limited to the columns the executor reads.</summary>
internal sealed record SchemaChangeJob
{
    /// <summary>The job's state, such as <c>RUNNING</c>, <c>FINISHED</c> or <c>CANCELLED</c>.</summary>
    public required string State { get; init; }

    /// <summary>StarRocks' message about the job, which explains a cancellation.</summary>
    public string? Msg { get; init; }
}
