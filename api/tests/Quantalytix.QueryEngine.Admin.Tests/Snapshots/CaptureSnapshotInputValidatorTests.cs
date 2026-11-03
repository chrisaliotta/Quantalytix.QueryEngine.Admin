using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Snapshots;

/// <summary>
/// Confirms the contract's <c>SnapshotKey</c> on capture: a letter, then letters, digits, underscores or dots, so the seed's dotted
/// snapshot keys such as <c>seed.assumptions.2024</c> can be captured through the API too; other punctuation and a leading digit or
/// dot are still refused, because requests name a snapshot by its key.
/// </summary>
public sealed class CaptureSnapshotInputValidatorTests
{
    /// <summary>Underscored and dotted keys are accepted.</summary>
    /// <param name="key">The key.</param>
    [Theory]
    [InlineData("fy2026_q3_close")]
    [InlineData("seed.assumptions.2027")]
    public void WellFormedKey_Passes(string key)
    {
        Assert.True(new CaptureSnapshotInputValidator().Validate(Input(key)).IsValid);
    }

    /// <summary>A key that starts with a digit or a dot, or holds other punctuation, fails on <c>Key</c>.</summary>
    /// <param name="key">The key.</param>
    [Theory]
    [InlineData("2027.assumptions")]
    [InlineData(".assumptions")]
    [InlineData("seed-assumptions")]
    public void MalformedKey_Fails(string key)
    {
        var error = Assert.Single(new CaptureSnapshotInputValidator().Validate(Input(key)).Errors);

        Assert.Equal(nameof(CaptureSnapshotInput.Key), error.PropertyName);
    }

    /// <summary>A capture under a key.</summary>
    private static CaptureSnapshotInput Input(string key)
    {
        return new CaptureSnapshotInput { Key = key, DisplayName = key, Description = null };
    }
}
