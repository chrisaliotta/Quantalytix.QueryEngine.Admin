using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Accepts a version content route only when the row belongs to the version the route names. The feature registration closes it
/// once per content kind, because assembly scanning skips open generic validators.
/// </summary>
/// <typeparam name="TItem">The kind of content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public sealed class VersionItemReferenceValidator<TItem, TInput> : AbstractValidator<VersionItemReference<TItem>>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="content">Reads the rows of the content kind.</param>
    public VersionItemReferenceValidator(IVersionContent<TItem, TInput> content)
    {
        RuleFor(reference => reference.ItemId)
            .MustAsync((reference, itemId, cancellationToken) => content.BelongsAsync(reference.VersionId, itemId, cancellationToken))
            .WithMessage(reference => $"Row {reference.ItemId} is not part of version {reference.VersionId}.");
    }
}
