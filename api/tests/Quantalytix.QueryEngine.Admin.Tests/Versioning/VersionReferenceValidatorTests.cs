using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms that the generic status endpoints (<c>PUT versions/{kind}/{id}/status</c>, <c>POST .../validation</c>) no longer move or
/// check any version kind: dimension, mapping and bridge versions are frozen through their own endpoints (OI-14), where a status
/// change would bypass the freeze's clone, note and pin; dataset versions and releases follow their strict lifecycle endpoints (D3,
/// R5), where a status change would bypass the release pins, the retire checks and the activation log. The endpoints go in P6.
/// </summary>
public sealed class VersionReferenceValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly VersionReferenceValidator _validator = new();

    /// <summary>Every kind is refused, because every kind has its own endpoints.</summary>
    /// <param name="kind">A version kind.</param>
    [Theory]
    [InlineData(VersionKinds.DimensionVersion)]
    [InlineData(VersionKinds.DatasetVersion)]
    [InlineData(VersionKinds.MappingVersion)]
    [InlineData(VersionKinds.BridgeVersion)]
    [InlineData(VersionKinds.SemanticRelease)]
    public void KindsWithTheirOwnEndpoints_AreRefused(string kind)
    {
        var result = _validator.TestValidate(new VersionReference { Kind = kind, Id = 1 });

        result.ShouldHaveValidationErrorFor(version => version.Kind);
    }
}
