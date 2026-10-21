namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// A design source's editable attributes, as <c>PUT report-versions/{versionId}/design/sources/{bridgeId}</c> takes them: the
/// dataset whose rows it classifies, the role the dataset binds the bound dimension through, and the measure that supplies each
/// variable.
/// </summary>
public record DesignSourceInput
{
    /// <summary>The source's label, which its bridge takes as its name.</summary>
    public required string Label { get; init; }

    /// <summary>The dataset; the release the report runs against decides its version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>A role of an analytical dimension that the dataset binds into members; its dimension is the source's bound dimension.</summary>
    public required long RoleId { get; init; }

    /// <summary>The dataset time field the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }

    /// <summary>Optional structured filter over the dataset's fields, in the query contract's <c>filterNode</c> vocabulary.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>The dataset measure that supplies each model variable the source fulfils; each variable appears once.</summary>
    public required IReadOnlyList<DesignVariableInput> Variables { get; init; }
}
