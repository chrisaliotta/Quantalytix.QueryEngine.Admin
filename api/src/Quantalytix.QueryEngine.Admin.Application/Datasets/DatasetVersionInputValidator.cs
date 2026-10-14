using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks a new dataset version. A clone names an existing version and no source object, because it keeps its source's;
/// an empty draft names a source object that the tenant database has, of the named kind.
/// </summary>
public sealed class DatasetVersionInputValidator : AbstractValidator<DatasetVersionInput>
{
    /// <summary>The message of a source object attribute sent with a clone.</summary>
    private const string CloneKeepsSourceMessage = "A clone keeps its source version's source object, so '{PropertyName}' must be empty.";

    /// <summary>Declares the rules.</summary>
    /// <param name="versions">The tenant's dataset versions, which a clone source must be one of.</param>
    /// <param name="sourceObjects">The tenant's source objects.</param>
    public DatasetVersionInputValidator(ITableGateway<DatasetVersionRecord> versions, SourceObjectService sourceObjects)
    {
        When(input => input.CloneFromVersionId is not null, () =>
        {
            RuleFor(input => input.CloneFromVersionId)
                .MustAsync(async (versionId, cancellationToken) =>
                    (await versions.ListAsync(new { DatasetVersionId = versionId }, cancellationToken)).Count == 1)
                .WithMessage(input => $"Dataset version {input.CloneFromVersionId} does not exist.");
            RuleFor(input => input.SourceObjectKind).Null().WithMessage(CloneKeepsSourceMessage);
            RuleFor(input => input.SourceObject).Null().WithMessage(CloneKeepsSourceMessage);
        }).Otherwise(() =>
        {
            RuleFor(input => input.SourceObjectKind)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MustBeIn(SourceObjectKinds.Definition)
                .DependentRules(() => RuleFor(input => input.SourceObject).MustBeSourceObject(input => input.SourceObjectKind, sourceObjects));
        });
    }
}
