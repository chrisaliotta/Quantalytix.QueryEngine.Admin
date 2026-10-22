using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the fields of a report style when it is written: clients render a style as it is stored, so the font size is a readable
/// 6 to 72 points, colors are <c>#RRGGBB</c>, weights, borders and the default role come from their vocabularies, and the indent is
/// 0 to 10 levels. The key is a stable key, because rows name the style by it.
/// </summary>
public sealed class ReportStyleInputValidatorTests
{
    /// <summary>The input validator.</summary>
    private readonly ReportStyleInputValidator _validator = new();

    /// <summary>A fully set style, and one that sets nothing but its name, pass.</summary>
    [Fact]
    public void WellFormedStyles_Pass()
    {
        _validator.TestValidate(Total()).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new ReportStyleInput { DisplayName = "Plain", IsItalic = false }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A font size outside 6 to 72 points, or with more than two decimals, fails.</summary>
    /// <param name="fontSize">The font size.</param>
    /// <param name="passes">Whether it passes.</param>
    [Theory]
    [InlineData(6, true)]
    [InlineData(72, true)]
    [InlineData(10.5, true)]
    [InlineData(5.99, false)]
    [InlineData(72.01, false)]
    [InlineData(10.125, false)]
    public void FontSizes_AreReadable(double fontSize, bool passes)
    {
        var result = _validator.TestValidate(Total() with { FontSize = (decimal)fontSize });

        if (passes) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(input => input.FontSize);
    }

    /// <summary>A color that is not six hexadecimal digits after '#' fails, whatever its case.</summary>
    /// <param name="color">The text and background color.</param>
    /// <param name="passes">Whether it passes.</param>
    [Theory]
    [InlineData("#1f2937", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("red", false)]
    [InlineData("#12345", false)]
    [InlineData("#1234567", false)]
    [InlineData("#12345G", false)]
    [InlineData("#123456\n", false)]
    public void Colors_AreHexadecimalTriplets(string color, bool passes)
    {
        var result = _validator.TestValidate(Total() with { TextColor = color, BackgroundColor = color });

        if (passes) result.ShouldNotHaveAnyValidationErrors();
        else
        {
            result.ShouldHaveValidationErrorFor(input => input.TextColor);
            result.ShouldHaveValidationErrorFor(input => input.BackgroundColor);
        }
    }

    /// <summary>A weight, border or default role outside its vocabulary, and an indent outside 0 to 10, each fail on their field.</summary>
    [Fact]
    public void VocabulariesAndIndent_AreChecked()
    {
        var result = _validator.TestValidate(Total() with { FontWeight = "heavy", BorderTop = "dotted", BorderBottom = "dashed", DefaultRole = "footer", Indent = 11 });

        result.ShouldHaveValidationErrorFor(input => input.FontWeight);
        result.ShouldHaveValidationErrorFor(input => input.BorderTop);
        result.ShouldHaveValidationErrorFor(input => input.BorderBottom);
        result.ShouldHaveValidationErrorFor(input => input.DefaultRole);
        result.ShouldHaveValidationErrorFor(input => input.Indent);
        Assert.Equal(5, result.Errors.Count);
        _validator.TestValidate(Total() with { Indent = -1 }).ShouldHaveValidationErrorFor(input => input.Indent);
    }

    /// <summary>A stable key passes and a malformed one fails.</summary>
    /// <param name="styleKey">The key the route names.</param>
    /// <param name="passes">Whether it passes.</param>
    [Theory]
    [InlineData("grand_total", true)]
    [InlineData("grand total", false)]
    [InlineData("_total", false)]
    public void Keys_AreStable(string styleKey, bool passes)
    {
        var result = new ReportStyleReferenceValidator().TestValidate(new ReportStyleReference { ReportId = ReportRows.ReportId, StyleKey = styleKey });

        if (passes) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(reference => reference.StyleKey);
    }

    /// <summary>A bold total style with a double rule below it, as the seed's reports have.</summary>
    private static ReportStyleInput Total()
    {
        return new ReportStyleInput
        {
            DisplayName = "Total",
            DefaultRole = RowRoles.Total,
            FontFamily = "Inter",
            FontSize = 11m,
            FontWeight = FontWeights.Bold,
            IsItalic = false,
            TextColor = "#111827",
            BackgroundColor = "#F3F4F6",
            BorderTop = BorderStyles.Thin,
            BorderBottom = BorderStyles.Double,
            Indent = 0
        };
    }
}
