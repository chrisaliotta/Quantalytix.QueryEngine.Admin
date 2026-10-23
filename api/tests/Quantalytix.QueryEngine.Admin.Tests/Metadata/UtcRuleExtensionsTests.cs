using System.Text.Json;
using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>
/// Confirms that metadata times are accepted only as UTC, written with a <c>Z</c> suffix. The tables store times without a
/// zone and the query engine reads them as UTC, so a JSON time without a zone is ambiguous, and one with an offset parses
/// as local time, which the StarRocks connection refuses to write. Every time input shares this rule; a range band's lower
/// datetime bound stands in for them, read from JSON the way a request body is.
/// </summary>
public sealed class UtcRuleExtensionsTests
{
    /// <summary>The validator under test.</summary>
    private readonly RangeBandsInputValidator _validator = new();

    /// <summary>A time with a <c>Z</c> suffix is UTC.</summary>
    [Fact]
    public void TimeWithAZSuffix_Passes()
    {
        var result = _validator.TestValidate(Bands("2026-07-01T00:00:00Z"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A time without a zone, or with an offset, even a zero one, is not read as UTC.</summary>
    /// <param name="time">The JSON time text.</param>
    [Theory]
    [InlineData("2026-07-01T00:00:00")]
    [InlineData("2026-07-01T00:00:00+00:00")]
    [InlineData("2026-07-01T02:00:00+02:00")]
    public void TimeWithoutAZSuffix_IsRejected(string time)
    {
        var result = _validator.TestValidate(Bands(time));

        result.ShouldHaveValidationErrorFor("Bands[0].LowerDatetime");
    }

    /// <summary>A band list of one band whose lower datetime bound is the given JSON time.</summary>
    private static RangeBandsInput Bands(string time)
    {
        var json = $$"""{"bands":[{"dimensionMemberId":1,"lowerDatetime":"{{time}}","lowerInclusive":true,"upperInclusive":false}]}""";
        return JsonSerializer.Deserialize<RangeBandsInput>(json, JsonSerializerOptions.Web)!;
    }
}
