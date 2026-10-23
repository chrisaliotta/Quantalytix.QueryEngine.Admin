using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>Accepts a binding route only when the binding belongs to the dataset version named in the same route.</summary>
public sealed class BindingReferenceValidator : AbstractValidator<BindingReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="bindings">The tenant's dimension bindings.</param>
    public BindingReferenceValidator(ITableGateway<DimensionBindingRecord> bindings)
    {
        RuleFor(reference => reference.DimensionBindingId)
            .MustAsync(async (reference, dimensionBindingId, cancellationToken) =>
                (await bindings.ListAsync(new { DimensionBindingId = dimensionBindingId, DatasetVersionId = reference.VersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Binding {reference.DimensionBindingId} is not a binding of dataset version {reference.VersionId}.");
    }
}
