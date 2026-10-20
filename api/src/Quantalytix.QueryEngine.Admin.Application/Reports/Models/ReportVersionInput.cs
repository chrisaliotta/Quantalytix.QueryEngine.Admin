namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The editable attributes of a report version, as <c>PUT report-versions/{versionId}</c> takes them.</summary>
public record ReportVersionInput
{
    /// <summary>The key of one of the version's profiles that requests run under when they name none; required at publication.</summary>
    public string? DefaultProfileKey { get; init; }

    /// <summary>A <c>subtotal_position</c> value: whether a member row that shows values precedes or follows its descendants.</summary>
    public required string SubtotalPosition { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional structured filter over calculation dimensions, the default where; its <c>parameter</c> operands every request supplies.</summary>
    public string? DefaultFilterJson { get; init; }

    /// <summary>Optional defaults: <c>periodicity</c>, <c>timeZone</c> and <c>layout</c> affect execution, other entries pass through.</summary>
    public string? DefaultSettingsJson { get; init; }

    /// <summary>Optional <c>rowDimensions</c> and <c>columnDimensions</c> lists of calculation-dimension keys.</summary>
    public string? DefaultPivotJson { get; init; }
}
