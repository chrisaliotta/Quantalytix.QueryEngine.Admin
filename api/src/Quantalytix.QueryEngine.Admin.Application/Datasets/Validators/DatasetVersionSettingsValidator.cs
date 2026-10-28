using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The shape of a version's settings: a known source kind and a named object, fitting text, and JSON properties.</summary>
public sealed class DatasetVersionSettingsValidator : AbstractValidator<DatasetVersionSettings>
{
    /// <summary>Declares the rules.</summary>
    public DatasetVersionSettingsValidator()
    {
        RuleFor(settings => settings.SourceObjectKind).MustBeIn(SourceObjectKinds.Definition);
        RuleFor(settings => settings.SourceObject).NotEmpty().MustFitVarchar(DatasetPatterns.IdentifierLength);
        RuleFor(settings => settings.DefaultTimeBindingId).MustBeChangeSetId();
        RuleFor(settings => settings.ExpressionLanguageVersion).NotEmpty().MustFitVarchar(32);
        RuleFor(settings => settings.SecurityPolicyKey).MustFitVarchar(128);
        RuleFor(settings => settings.Properties).MustBeJson().When(settings => !string.IsNullOrWhiteSpace(settings.Properties));
    }
}
