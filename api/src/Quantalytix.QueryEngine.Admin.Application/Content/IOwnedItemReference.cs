namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>A content route's owner and one of its rows, which <see cref="OwnedItemReferenceValidator{TReference, TItem, TInput}"/> checks belong together.</summary>
public interface IOwnedItemReference
{
    /// <summary>What owns the row, as messages name it, such as <c>version</c>.</summary>
    string OwnerName { get; }

    /// <summary>The owner's ID.</summary>
    long OwnerId { get; }

    /// <summary>The row's ID.</summary>
    long ItemId { get; }
}
