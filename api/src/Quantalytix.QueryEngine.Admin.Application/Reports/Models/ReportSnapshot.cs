using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The report change-set aggregate held in memory: the report's content and what its operations consult around it.</summary>
public sealed record ReportSnapshot
{
    /// <summary>The report with its rows, sources and content, and its model's metrics and parameters.</summary>
    public required ReportGraph Content { get; init; }

    /// <summary>What the operations read once outside the report.</summary>
    public required ReportSurroundings Surroundings { get; init; }
}

/// <summary>What a report change set reads once outside the report.</summary>
public sealed record ReportSurroundings
{
    /// <summary>The report dimension's one version, which holds the member rows.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Every role of the tenant, which sources, calculation dimensions and scenarios name.</summary>
    public required IReadOnlyList<DimensionRoleRecord> Roles { get; init; }

    /// <summary>Every dataset of the tenant by id, which sources name.</summary>
    public required IReadOnlySet<long> DatasetIds { get; init; }
}
