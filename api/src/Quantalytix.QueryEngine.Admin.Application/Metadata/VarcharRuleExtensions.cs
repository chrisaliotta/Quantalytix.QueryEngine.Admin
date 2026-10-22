using System.Text;
using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>FluentValidation rules that keep free text within the VARCHAR columns of the metadata tables.</summary>
public static class VarcharRuleExtensions
{
    /// <summary>
    /// Requires text to fit a VARCHAR column of the given length. StarRocks counts that length in UTF-8 bytes and refuses
    /// an INSERT or UPDATE whose text is longer, so text with characters outside ASCII can be too long while it has fewer
    /// characters than the length. Null passes, as with FluentValidation's built-in rules. Text held to an ASCII pattern,
    /// such as a key, has one byte per character and can keep <c>MaximumLength</c>.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a string property of the input.</param>
    /// <param name="length">The column's declared VARCHAR length.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustFitVarchar<T>(this IRuleBuilder<T, string?> rule, int length)
    {
        return rule
            .Must(text => text is null || Fits(text, length))
            .WithMessage($"'{{PropertyName}}' must fit in {length} bytes of UTF-8 text; characters outside ASCII take 2 to 4 bytes each.");
    }

    /// <summary>Whether text fits a VARCHAR column of the given length, which StarRocks counts in UTF-8 bytes.</summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The column's declared VARCHAR length.</param>
    /// <returns>True when the text's UTF-8 bytes are at most the length.</returns>
    public static bool Fits(string text, int length)
    {
        return Encoding.UTF8.GetByteCount(text) <= length;
    }
}
