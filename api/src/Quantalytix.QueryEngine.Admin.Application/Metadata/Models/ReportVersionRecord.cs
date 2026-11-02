namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report_version</c> row: one snapshot's settings of a report and the calculation model whose metrics it shows. The report's
/// content rows key by this version.
/// </summary>
public sealed record ReportVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>The snapshot the version belongs to; unique together with the report.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>margin_analysis</c>.</summary>
    public required string ReportType { get; init; }

    /// <summary>The calculation model whose metrics the report reads; fixed at creation.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>The key of the profile a request runs under when it names none; required to enable the report.</summary>
    public string? DefaultProfileKey { get; init; }

    /// <summary>A <c>subtotal_position</c> value: whether a member row that shows values precedes or follows its descendants.</summary>
    public required string SubtotalPosition { get; init; }

    /// <summary>Whether the engine offers the report; an enabled report always passes the report rules.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Optional payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional filter over calculation dimensions, ANDed with every request's.</summary>
    public string? DefaultFilterJson { get; init; }

    /// <summary>Optional defaults; only <c>periodicity</c>, <c>timeZone</c> and <c>layout</c> affect execution.</summary>
    public string? DefaultSettingsJson { get; init; }

    /// <summary>Optional <c>rowDimensions</c> and <c>columnDimensions</c>, whose union is the default breakout.</summary>
    public string? DefaultPivotJson { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}
