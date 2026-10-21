namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>One item of a bridge source that maps to a row.</summary>
public sealed record SourceItemInput
{
    /// <summary>The key of an assignable member of the published version of the source's item dimension.</summary>
    public required string ItemKey { get; init; }

    /// <summary>The signed coefficient the item's values take in the row, such as -1 to flip a sign.</summary>
    public required decimal Factor { get; init; }
}
