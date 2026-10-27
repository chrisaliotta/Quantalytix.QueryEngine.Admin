using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Accepts only the version kinds the workflow has a store for, except dimension versions: a dimension version is frozen and
/// checked through its own endpoints (<c>dimension-versions/{versionId}/freeze</c> and <c>/check</c>), never moved by status.
/// </summary>
public sealed class VersionReferenceValidator : AbstractValidator<VersionReference>
{
    /// <summary>Declares the rules.</summary>
    public VersionReferenceValidator()
    {
        RuleFor(version => version.Kind)
            .MustBeIn(VersionKinds.Definition)
            .NotEqual(VersionKinds.DimensionVersion)
            .WithMessage("Dimension versions are frozen through POST dimension-versions/{versionId}/freeze and checked through POST dimension-versions/{versionId}/check.");
    }
}
