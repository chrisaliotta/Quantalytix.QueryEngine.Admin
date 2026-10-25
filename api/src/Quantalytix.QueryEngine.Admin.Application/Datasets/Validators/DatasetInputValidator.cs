using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks a new dataset: its identity attributes, a well-formed dataset key that no other dataset uses, and a first source
/// object that the tenant database has, of the named kind.
/// </summary>
public sealed class DatasetInputValidator : AbstractValidator<DatasetInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="datasets">The tenant's datasets, which the new key must not repeat.</param>
    /// <param name="sourceObjects">The tenant's source objects.</param>
    public DatasetInputValidator(ITableGateway<DatasetRecord> datasets, SourceObjectService sourceObjects)
    {
        Include(new DatasetUpdateInputValidator());
        RuleFor(input => input.DatasetKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(DatasetPatterns.DatasetKey())
            .WithMessage(DatasetPatterns.DatasetKeyMessage)
            .MustAsync(async (key, cancellationToken) => (await datasets.ListAsync(new { DatasetKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"Dataset key '{input.DatasetKey}' is already used by another dataset.");
        RuleFor(input => input.SourceObjectKind)
            .MustBeIn(SourceObjectKinds.Definition)
            .DependentRules(() => RuleFor(input => input.SourceObject).MustBeSourceObject(input => input.SourceObjectKind, sourceObjects));
    }
}
