namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_version</c> row: one immutable version of a report: its dimension, model version, defaults, lines and columns.</summary>
public sealed record ReportVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>Version number within the report.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The canonical dimension whose members the member lines name.</summary>
    public required long ReportDimensionId { get; init; }

    /// <summary>The calculation model version the report reads.</summary>
    public long? ModelVersionId { get; init; }

    /// <summary>The profile a request runs under when it names none.</summary>
    public long? DefaultSourceProfileId { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional filter over calculation dimensions, ANDed with every request's.</summary>
    public string? DefaultFilterJson { get; init; }

    /// <summary>Optional defaults; only <c>periodicity</c> and <c>timeZone</c> affect execution.</summary>
    public string? DefaultSettingsJson { get; init; }

    /// <summary>Optional <c>rowDimensions</c> and <c>columnDimensions</c>, whose union is the default breakout.</summary>
    public string? DefaultPivotJson { get; init; }
}
