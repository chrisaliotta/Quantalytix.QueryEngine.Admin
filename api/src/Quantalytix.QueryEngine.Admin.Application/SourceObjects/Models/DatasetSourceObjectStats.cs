namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>The contract's <c>DatasetSourceObjectStats</c>: a table's or view's size, read from <c>information_schema</c> (OI-34).</summary>
public sealed record DatasetSourceObjectStats
{
    /// <summary>The row count StarRocks reports.</summary>
    public required long RowCount { get; init; }

    /// <summary>The on-disk size in bytes; 0 for a view that stores nothing.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>When the statistics were read (UTC).</summary>
    public required DateTime MeasuredAt { get; init; }
}
