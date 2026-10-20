namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The editable attributes of a report version, as <c>PUT report-versions/{versionId}</c> takes them.</summary>
public record ReportVersionInput
{
    /// <summary>The key of a profile of the model's published version that requests run under when they name none.</summary>
    public string? DefaultProfileKey { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional structured filter over calculation dimensions, ANDed with every request's.</summary>
    public string? DefaultFilterJson { get; init; }

    /// <summary>Optional defaults: <c>periodicity</c> and <c>timeZone</c> affect execution, other entries pass through to clients.</summary>
    public string? DefaultSettingsJson { get; init; }

    /// <summary>Optional <c>rowDimensions</c> and <c>columnDimensions</c> lists of calculation-dimension keys.</summary>
    public string? DefaultPivotJson { get; init; }
}
