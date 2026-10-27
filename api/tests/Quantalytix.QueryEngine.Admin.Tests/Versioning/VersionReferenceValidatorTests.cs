using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms that the generic status endpoints (<c>PUT versions/{kind}/{id}/status</c>, <c>POST .../validation</c>) no longer move or
/// check dimension versions: a dimension version is frozen and checked through its own endpoints (OI-14), and a status change
/// would bypass the freeze's clone, note and pin. The other kinds still use them until their modules move (P6).
/// </summary>
public sealed class VersionReferenceValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly VersionReferenceValidator _validator = new();

    /// <summary>A dimension version is refused.</summary>
    [Fact]
    public void DimensionVersion_IsRefused()
    {
        var result = _validator.TestValidate(new VersionReference { Kind = VersionKinds.DimensionVersion, Id = 1 });

        result.ShouldHaveValidationErrorFor(version => version.Kind);
    }

    /// <summary>The kinds whose modules have not moved yet still pass.</summary>
    /// <param name="kind">A version kind.</param>
    [Theory]
    [InlineData(VersionKinds.DatasetVersion)]
    [InlineData(VersionKinds.MappingVersion)]
    [InlineData(VersionKinds.BridgeVersion)]
    [InlineData(VersionKinds.SemanticRelease)]
    public void OtherKinds_Pass(string kind)
    {
        var result = _validator.TestValidate(new VersionReference { Kind = kind, Id = 1 });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
