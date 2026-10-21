namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>A report content route: the report and one of its rows.</summary>
/// <typeparam name="TItem">The kind of content row, which selects the validator that checks the row is the report's.</typeparam>
public sealed record ReportItemReference<TItem> : IOwnedItemReference
{
    /// <summary>The report that owns the row.</summary>
    public required long ReportId { get; init; }

    /// <summary>The row.</summary>
    public required long ItemId { get; init; }

    /// <summary>The owner's name in messages.</summary>
    string IOwnedItemReference.OwnerName => "report";

    /// <summary>The report.</summary>
    long IOwnedItemReference.OwnerId => ReportId;
}
