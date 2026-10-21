namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>A report source's editable attributes, as <c>PUT reports/{reportId}/sources/{sourceId}</c> takes them.</summary>
public record ReportSourceInput
{
    /// <summary>The source's label, which a bridge source's bridge takes as its name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dataset field, with a time binding, the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }
}
