namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>Every source item that maps to one row of a report design, as <c>PUT report-versions/{versionId}/design/row-mappings</c> takes it.</summary>
public sealed record RowMappingsInput
{
    /// <summary>The data row the items map to.</summary>
    public required string RowKey { get; init; }

    /// <summary>The row's complete mapping list: the row's entries the list leaves out are deleted, and a listed item mapped to another row moves here.</summary>
    public required IReadOnlyList<RowMappingInput> Mappings { get; init; }
}
