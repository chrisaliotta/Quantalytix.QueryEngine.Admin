using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>
/// Accepts a content route only when the row belongs to the owner the route names. The feature registration closes it once per
/// content kind and route, because assembly scanning skips open generic validators.
/// </summary>
/// <typeparam name="TReference">The route: a model version's or a report's.</typeparam>
/// <typeparam name="TItem">The kind of content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public sealed class OwnedItemReferenceValidator<TReference, TItem, TInput> : AbstractValidator<TReference>
    where TReference : IOwnedItemReference
{
    /// <summary>Declares the rules.</summary>
    /// <param name="content">Reads the rows of the content kind.</param>
    public OwnedItemReferenceValidator(IOwnedContent<TItem, TInput> content)
    {
        RuleFor(reference => reference.ItemId)
            .MustAsync((reference, itemId, cancellationToken) => content.BelongsAsync(reference.OwnerId, itemId, cancellationToken))
            .WithMessage(reference => $"Row {reference.ItemId} is not part of {reference.OwnerName} {reference.OwnerId}.");
    }
}
