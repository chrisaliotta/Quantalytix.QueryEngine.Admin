using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a new model version: its attributes, and that the version it clones is a version of the route's model.</summary>
public sealed class NewModelVersionInputValidator : AbstractValidator<NewModelVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="versions">Reads the clone source.</param>
    /// <param name="route">Names the model the new version belongs to.</param>
    public NewModelVersionInputValidator(ITableGateway<CalculationModelVersionRecord> versions, IRequestRoute route)
    {
        Include(new ModelVersionInputValidator());
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { ModelVersionId = versionId, ModelId = route.Id("modelId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Model version {input.CloneFromVersionId} is not a version of this model.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
