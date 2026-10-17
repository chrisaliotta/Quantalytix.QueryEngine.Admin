using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Accepts a model content route only when the row belongs to the model version the route names. The feature registration
/// closes it once per content kind, because assembly scanning skips open generic validators.
/// </summary>
/// <typeparam name="TItem">The kind of content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public sealed class ModelItemReferenceValidator<TItem, TInput> : AbstractValidator<ModelItemReference<TItem>>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="content">Reads the rows of the content kind.</param>
    public ModelItemReferenceValidator(IModelVersionContent<TItem, TInput> content)
    {
        RuleFor(reference => reference.ItemId)
            .MustAsync((reference, itemId, cancellationToken) => content.BelongsAsync(reference.VersionId, itemId, cancellationToken))
            .WithMessage(reference => $"Row {reference.ItemId} is not part of model version {reference.VersionId}.");
    }
}
