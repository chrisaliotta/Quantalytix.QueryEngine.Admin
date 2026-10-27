using System.Text.Json;
using FluentValidation;
using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>
/// Confirms that metadata times are accepted only as UTC, written with a <c>Z</c> suffix. The tables store times without a
/// zone and the query engine reads them as UTC, so a JSON time without a zone is ambiguous, and one with an offset parses
/// as local time, which the StarRocks connection refuses to write. Every time input shares this rule; a time property read
/// from JSON the way a request body is stands in for them.
/// </summary>
public sealed class UtcRuleExtensionsTests
{
    /// <summary>The validator under test.</summary>
    private readonly InlineValidator<Window> _validator = new() { validator => validator.RuleFor(window => window.Start).MustBeUtc() };

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

        result.ShouldHaveValidationErrorFor(window => window.Start);
    }

    /// <summary>A window whose start is the given JSON time.</summary>
    private static Window Bands(string time)
    {
        return JsonSerializer.Deserialize<Window>($$"""{"start":"{{time}}"}""", JsonSerializerOptions.Web)!;
    }

    /// <summary>An input with one time.</summary>
    private sealed record Window
    {
        /// <summary>The time.</summary>
        public required DateTime Start { get; init; }
    }
}
