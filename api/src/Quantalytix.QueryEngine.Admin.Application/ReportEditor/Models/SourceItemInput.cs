namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>One rule of a bridge source that adds into a row: an item, or every item under a member, with a factor.</summary>
public sealed record SourceItemInput
{
    /// <summary>
    /// The key of a member of the published version of the source's item dimension: an assignable member when the rule takes the
    /// member alone, and any member when it takes every member under it.
    /// </summary>
    public required string ItemKey { get; init; }

    /// <summary>A <c>source_selection</c> value: the member alone, or the member and every member under it.</summary>
    public required string Selection { get; init; }

    /// <summary>The coefficient the rule's values take in the row, such as 0.5 to take half of them or -1 to take them back out.</summary>
    public required decimal Factor { get; init; }
}
