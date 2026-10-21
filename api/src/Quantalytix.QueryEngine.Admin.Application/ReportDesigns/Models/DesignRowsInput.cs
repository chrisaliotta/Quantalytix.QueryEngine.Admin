namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>A report design's complete row tree, as <c>PUT report-versions/{versionId}/design/rows</c> takes it.</summary>
public sealed record DesignRowsInput
{
    /// <summary>Every row; a stored row the list leaves out is deleted, and a key the design does not hold yet is a new row.</summary>
    public required IReadOnlyList<DesignRowInput> Rows { get; init; }
}
