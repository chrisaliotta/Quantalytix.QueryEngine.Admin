using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The shape of an empty draft: a table or view of the tenant database.</summary>
public sealed class DatasetCreateEmptyDraftInputValidator : AbstractValidator<DatasetCreateEmptyDraftInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="sourceObjects">The tables and views datasets can read.</param>
    public DatasetCreateEmptyDraftInputValidator(SourceObjectDirectory sourceObjects)
    {
        RuleFor(input => input.SourceObjectKind)
            .MustBeIn(SourceObjectKinds.Definition)
            .DependentRules(() => RuleFor(input => input.SourceObject)
                .MustAsync(async (input, name, cancellationToken) => (await sourceObjects.ObjectsAsync(cancellationToken)).Any(item => item.Kind == input.SourceObjectKind && item.Name == name))
                .WithMessage(input => $"'{input.SourceObject}' is not a {input.SourceObjectKind} of the tenant database."));
    }
}
