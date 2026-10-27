namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionOwnerReport</c>: the report a report dimension holds the rows of.</summary>
public sealed record DimensionOwnerReport
{
    /// <summary>The report key.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The report's display name.</summary>
    public required string ReportName { get; init; }
}
