namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>One source item that maps to a row.</summary>
public sealed record RowMappingInput
{
    /// <summary>The design source's bridge.</summary>
    public required long BridgeId { get; init; }

    /// <summary>The key of an assignable member of the published version of the source's bound dimension.</summary>
    public required string ItemKey { get; init; }

    /// <summary>The signed coefficient the item's values take in the row, such as -1 to flip a sign.</summary>
    public required decimal Factor { get; init; }
}
