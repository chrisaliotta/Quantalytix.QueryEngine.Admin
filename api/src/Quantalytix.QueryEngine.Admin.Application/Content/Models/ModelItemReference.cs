namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>A model content route: the calculation model and one of its rows.</summary>
/// <typeparam name="TItem">The kind of content row, which selects the validator that checks the row is the model's.</typeparam>
public sealed record ModelItemReference<TItem> : IOwnedItemReference
{
    /// <summary>The model that owns the row.</summary>
    public required long ModelId { get; init; }

    /// <summary>The row.</summary>
    public required long ItemId { get; init; }

    /// <summary>The owner's name in messages.</summary>
    string IOwnedItemReference.OwnerName => "model";

    /// <summary>The model.</summary>
    long IOwnedItemReference.OwnerId => ModelId;
}
