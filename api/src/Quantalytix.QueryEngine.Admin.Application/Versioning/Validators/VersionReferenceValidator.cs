using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Accepts only the version kinds the workflow has a store for, except the kinds whose module moves and checks them through its own
/// endpoints: a dimension version is frozen (<c>dimension-versions/{versionId}/freeze</c>), and a dataset version goes through its
/// strict lifecycle (<c>dataset-versions/{versionId}/validate</c>, <c>/publish</c>, <c>/reopen</c>, <c>/retire</c>).
/// </summary>
public sealed class VersionReferenceValidator : AbstractValidator<VersionReference>
{
    /// <summary>Where each kind with its own endpoints is moved and checked instead.</summary>
    private static readonly Dictionary<string, string> OwnEndpoints = new(StringComparer.Ordinal)
    {
        [VersionKinds.DimensionVersion] =
            "Dimension versions are frozen through POST dimension-versions/{versionId}/freeze and checked through POST dimension-versions/{versionId}/check.",
        [VersionKinds.DatasetVersion] =
            "Dataset versions move through POST dataset-versions/{versionId}/validate, /publish, /reopen and /retire, and are checked through POST dataset-versions/{versionId}/check."
    };

    /// <summary>Declares the rules.</summary>
    public VersionReferenceValidator()
    {
        RuleFor(version => version.Kind)
            .Must(kind => !OwnEndpoints.ContainsKey(kind))
            .WithMessage((_, kind) => OwnEndpoints[kind])
            .MustBeIn(VersionKinds.Definition);
    }
}
