using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms that the generic status endpoints (<c>PUT versions/{kind}/{id}/status</c>, <c>POST .../validation</c>) no longer move or
/// check dimension and dataset versions: a dimension version is frozen through its own endpoint (OI-14), where a status change would
/// bypass the freeze's clone, note and pin; a dataset version follows its strict lifecycle endpoints (D3), where a status change would
/// bypass the release pins and the retire checks. The other kinds still use them until their modules move (P6).
/// </summary>
public sealed class VersionReferenceValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly VersionReferenceValidator _validator = new();

    /// <summary>The kinds with their own endpoints are refused.</summary>
    /// <param name="kind">A version kind.</param>
    [Theory]
    [InlineData(VersionKinds.DimensionVersion)]
    [InlineData(VersionKinds.DatasetVersion)]
    public void KindsWithTheirOwnEndpoints_AreRefused(string kind)
    {
        var result = _validator.TestValidate(new VersionReference { Kind = kind, Id = 1 });

        result.ShouldHaveValidationErrorFor(version => version.Kind);
    }

    /// <summary>The kinds whose modules have not moved yet still pass.</summary>
    /// <param name="kind">A version kind.</param>
    [Theory]
    [InlineData(VersionKinds.MappingVersion)]
    [InlineData(VersionKinds.BridgeVersion)]
    [InlineData(VersionKinds.SemanticRelease)]
    public void OtherKinds_Pass(string kind)
    {
        var result = _validator.TestValidate(new VersionReference { Kind = kind, Id = 1 });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
