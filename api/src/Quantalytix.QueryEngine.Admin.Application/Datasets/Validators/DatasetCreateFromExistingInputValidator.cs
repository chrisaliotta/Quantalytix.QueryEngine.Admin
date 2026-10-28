using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The shape of a dataset over an existing object: a dataset key, a display name, and a table or view of the tenant database. A
/// taken key is a conflict the command decides.
/// </summary>
public sealed class DatasetCreateFromExistingInputValidator : AbstractValidator<DatasetCreateFromExistingInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="sourceObjects">The tables and views datasets can read.</param>
    public DatasetCreateFromExistingInputValidator(SourceObjectDirectory sourceObjects)
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeDottedKey(DatasetPatterns.DatasetKeyLength);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.SourceObjectKind)
            .MustBeIn(SourceObjectKinds.Definition)
            .DependentRules(() => RuleFor(input => input.SourceObject)
                .MustAsync(async (input, name, cancellationToken) => (await sourceObjects.ObjectsAsync(cancellationToken)).Any(item => item.Kind == input.SourceObjectKind && item.Name == name))
                .WithMessage(input => $"'{input.SourceObject}' is not a {input.SourceObjectKind} of the tenant database."));
    }
}
