using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Accepts a field route only when the field belongs to the dataset version named in the same route.</summary>
public sealed class FieldReferenceValidator : AbstractValidator<FieldReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="fields">The tenant's fields.</param>
    public FieldReferenceValidator(ITableGateway<FieldRecord> fields)
    {
        RuleFor(reference => reference.FieldId)
            .MustAsync(async (reference, fieldId, cancellationToken) =>
                (await fields.ListAsync(new { FieldId = fieldId, DatasetVersionId = reference.VersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Field {reference.FieldId} is not a field of dataset version {reference.VersionId}.");
    }
}
