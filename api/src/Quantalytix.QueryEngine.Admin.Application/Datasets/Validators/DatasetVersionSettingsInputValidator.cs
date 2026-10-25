using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks the settings of a draft dataset version: a source object that the tenant database has, of the named kind, and
/// properties that parse as JSON. Whether the default time binding belongs to the version is a publication rule.
/// </summary>
public sealed class DatasetVersionSettingsInputValidator : AbstractValidator<DatasetVersionSettingsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="sourceObjects">The tenant's source objects.</param>
    public DatasetVersionSettingsInputValidator(SourceObjectService sourceObjects)
    {
        RuleFor(input => input.SourceObjectKind)
            .MustBeIn(SourceObjectKinds.Definition)
            .DependentRules(() => RuleFor(input => input.SourceObject).MustBeSourceObject(input => input.SourceObjectKind, sourceObjects));
        RuleFor(input => input.ExpressionLanguageVersion).NotEmpty().MustFitVarchar(32);
        RuleFor(input => input.SecurityPolicyKey).MustFitVarchar(128);
        RuleFor(input => input.Properties).MustBeJson();
    }
}
