namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The editable attributes of a draft report version, as <c>PUT report-versions/{versionId}</c> takes them.</summary>
public record ReportVersionInput
{
    /// <summary>A profile of the version's model version that requests run under when they name none.</summary>
    public long? DefaultSourceProfileId { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional structured filter over calculation dimensions, ANDed with every request's.</summary>
    public string? DefaultFilterJson { get; init; }

    /// <summary>Optional defaults: <c>periodicity</c> and <c>timeZone</c> affect execution, other entries pass through to clients.</summary>
    public string? DefaultSettingsJson { get; init; }

    /// <summary>Optional <c>rowDimensions</c> and <c>columnDimensions</c> lists of calculation-dimension keys.</summary>
    public string? DefaultPivotJson { get; init; }
}
