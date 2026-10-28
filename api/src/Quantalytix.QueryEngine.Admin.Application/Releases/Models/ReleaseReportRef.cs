namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseReportRef</c>: what a report reads from a release (its sources) and from an assumption set.</summary>
public sealed record ReleaseReportRef
{
    /// <summary>The report.</summary>
    public required long Id { get; init; }

    /// <summary>The report key.</summary>
    public required string Key { get; init; }

    /// <summary>The report's display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the report is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The report's sources, by key.</summary>
    public required IReadOnlyList<ReleaseReportSource> Sources { get; init; }

    /// <summary>The analytical identities the report reads.</summary>
    public required IReadOnlyList<ReleaseReportRead> Reads { get; init; }
}

/// <summary>The contract's <c>ReleaseReportSource</c>: a report source's dataset and measures.</summary>
public sealed record ReleaseReportSource
{
    /// <summary>The source key.</summary>
    public required string Key { get; init; }

    /// <summary>The dataset the source reads.</summary>
    public required long DatasetId { get; init; }

    /// <summary>Whether the source adds items into rows with factors other than 1, through its bridge.</summary>
    public required bool UsesFactors { get; init; }

    /// <summary>The measures the source's variables read.</summary>
    public required IReadOnlyList<string> MeasureKeys { get; init; }
}

/// <summary>The contract's <c>ReleaseReportRead</c>: an analytical identity a report reads.</summary>
public sealed record ReleaseReportRead
{
    /// <summary>A <c>PinKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The dimension, mapping set or bridge.</summary>
    public required long IdentityId { get; init; }
}
