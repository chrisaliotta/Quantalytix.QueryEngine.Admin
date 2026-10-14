using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Accepts only the version kinds the workflow has a store for.</summary>
public sealed class VersionReferenceValidator : AbstractValidator<VersionReference>
{
    /// <summary>Declares the rules.</summary>
    public VersionReferenceValidator()
    {
        RuleFor(version => version.Kind).MustBeIn(VersionKinds.Definition);
    }
}
