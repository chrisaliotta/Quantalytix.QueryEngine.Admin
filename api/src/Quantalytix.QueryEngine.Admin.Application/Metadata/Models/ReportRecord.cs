namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report</c> row: a report, its settings and the calculation model whose metrics it shows. Reports are not versioned:
/// the engine reads an enabled report as it stands, with its model as it stands and the published version of every dimension and
/// bridge it names.
/// </summary>
public sealed record ReportRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>Stable logical report identity.</summary>
    public required string ReportKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>margin_analysis</c>.</summary>
    public required string ReportType { get; init; }

    /// <summary>The report's own dimension, whose members are its rows and into which every source classifies; fixed at creation.</summary>
    public required long ReportDimensionId { get; init; }

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
}
