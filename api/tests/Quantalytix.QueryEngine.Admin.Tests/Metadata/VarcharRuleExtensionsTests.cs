using FluentValidation;
using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>
/// Confirms that free-text limits count UTF-8 bytes, as StarRocks counts VARCHAR lengths: the live sandbox refused an
/// INSERT of three two-byte characters into a VARCHAR(4) column ("Insert has filtered data"), so a display name with
/// fewer characters than its column's length but more bytes reached the database and failed as a 500. The rule answers
/// such text with a 400 instead, and still accepts text that fills the column exactly. A dimension's VARCHAR(256) display
/// name stands in for every free-text column.
/// </summary>
public sealed class VarcharRuleExtensionsTests
{
    /// <summary>The validator under test.</summary>
    private readonly InlineValidator<Named> _validator = new() { validator => validator.RuleFor(input => input.DisplayName).MustFitVarchar(256) };

    /// <summary>Two hundred two-byte characters are 400 bytes, too long for VARCHAR(256) though only 200 characters.</summary>
    [Fact]
    public void TextWithMoreBytesThanTheColumnHolds_IsRejected()
    {
        var result = _validator.TestValidate(Dimension(new string('é', 200)));

        result.ShouldHaveValidationErrorFor(input => input.DisplayName);
    }

    /// <summary>One hundred twenty-eight two-byte characters are exactly the 256 bytes the column holds.</summary>
    [Fact]
    public void TextThatFillsTheColumnInBytes_Passes()
    {
        var result = _validator.TestValidate(Dimension(new string('é', 128)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>An input with the given display name.</summary>
    private static Named Dimension(string displayName)
    {
        return new Named { DisplayName = displayName };
    }

    /// <summary>An input with a display name.</summary>
    private sealed record Named
    {
        /// <summary>The display name.</summary>
        public required string DisplayName { get; init; }
    }
}
