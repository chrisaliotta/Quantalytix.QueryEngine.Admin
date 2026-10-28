using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Accepts only the version kinds the workflow has a store for, except the kinds whose module moves and checks them through its own
/// endpoints: a dimension, mapping or bridge version is frozen (<c>…-versions/{versionId}/freeze</c>), and a dataset version or a
/// release goes through its strict lifecycle (<c>/validate</c>, <c>/publish</c>, <c>/reopen</c>, <c>/retire</c>). Every kind now has
/// its own endpoints, so the generic status endpoints refuse every request.
/// </summary>
public sealed class VersionReferenceValidator : AbstractValidator<VersionReference>
{
    /// <summary>Where each kind with its own endpoints is moved and checked instead.</summary>
    private static readonly Dictionary<string, string> OwnEndpoints = new(StringComparer.Ordinal)
    {
        [VersionKinds.DimensionVersion] =
            "Dimension versions are frozen through POST dimension-versions/{versionId}/freeze and checked through POST dimension-versions/{versionId}/check.",
        [VersionKinds.DatasetVersion] =
            "Dataset versions move through POST dataset-versions/{versionId}/validate, /publish, /reopen and /retire, and are checked through POST dataset-versions/{versionId}/check.",
        [VersionKinds.MappingVersion] =
            "Mapping versions are frozen through POST mapping-versions/{mappingVersionId}/freeze and checked through POST mapping-versions/{mappingVersionId}/check.",
        [VersionKinds.BridgeVersion] =
            "Bridge versions are frozen through POST bridge-versions/{bridgeVersionId}/freeze and checked through POST bridge-versions/{bridgeVersionId}/check.",
        [VersionKinds.SemanticRelease] =
            "Releases move through POST releases/{releaseId}/validate, /publish, /reopen, /retire and /activate, and are checked through POST releases/{releaseId}/check."
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
