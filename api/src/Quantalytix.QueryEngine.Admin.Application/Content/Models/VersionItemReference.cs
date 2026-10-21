namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>A model version content route: the version and one of its rows.</summary>
/// <typeparam name="TItem">The kind of content row, which selects the validator that checks the row is the version's.</typeparam>
public sealed record VersionItemReference<TItem> : IOwnedItemReference
{
    /// <summary>The version that owns the row.</summary>
    public required long VersionId { get; init; }

    /// <summary>The row.</summary>
    public required long ItemId { get; init; }

    /// <summary>The owner's name in messages.</summary>
    string IOwnedItemReference.OwnerName => "version";

    /// <summary>The version.</summary>
    long IOwnedItemReference.OwnerId => VersionId;
}
