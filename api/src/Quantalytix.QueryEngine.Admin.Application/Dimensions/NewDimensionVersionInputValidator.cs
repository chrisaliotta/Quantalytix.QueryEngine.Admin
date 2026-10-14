using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks a new dimension version: its attributes, and that the version it clones exists.</summary>
public sealed class NewDimensionVersionInputValidator : AbstractValidator<NewDimensionVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="versions">Reads the clone source.</param>
    public NewDimensionVersionInputValidator(ITableGateway<DimensionVersionRecord> versions)
    {
        Include(new DimensionVersionInputValidator());
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { DimensionVersionId = versionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension version {input.CloneFromVersionId} does not exist.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
