using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks a new dimension version: its attributes, and that the version it clones is a version of the route's dimension.</summary>
public sealed class NewDimensionVersionInputValidator : AbstractValidator<NewDimensionVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="versions">Reads the clone source.</param>
    /// <param name="route">Names the dimension the new version belongs to.</param>
    public NewDimensionVersionInputValidator(ITableGateway<DimensionVersionRecord> versions, IRequestRoute route)
    {
        Include(new DimensionVersionInputValidator());
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { DimensionVersionId = versionId, DimensionId = route.Id("dimensionId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension version {input.CloneFromVersionId} is not a version of this dimension.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
