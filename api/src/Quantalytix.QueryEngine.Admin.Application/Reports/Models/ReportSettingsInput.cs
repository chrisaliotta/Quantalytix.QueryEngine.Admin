namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The settings of a report, as <c>PUT reports/{reportId}/settings</c> takes them.</summary>
public sealed record ReportSettingsInput
{
    /// <summary>The key of the profile a request runs under when it names none, one of the report's; required to enable the report.</summary>
    public string? DefaultProfileKey { get; init; }

    /// <summary>A <c>subtotal_position</c> value.</summary>
    public required string SubtotalPosition { get; init; }

    /// <summary>Optional filter over calculation dimensions and request parameters, ANDed with every request's.</summary>
    public string? DefaultFilterJson { get; init; }

    /// <summary>Optional defaults; only <c>periodicity</c>, <c>timeZone</c> and <c>layout</c> affect execution.</summary>
    public string? DefaultSettingsJson { get; init; }

    /// <summary>Optional <c>rowDimensions</c> and <c>columnDimensions</c>, whose union is the default breakout.</summary>
    public string? DefaultPivotJson { get; init; }
}
