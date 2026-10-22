using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>formula_parameters_resolve</c>, which the engine trusts: it evaluates formulas without checking types, so every
/// <c>@name</c> must be a context or declared parameter, numbers must sit where numbers belong, and text, a string literal or a text
/// parameter, only where text belongs: compared with text by =, &lt;&gt; or IN, or as YEARFRAC's day count convention.
/// </summary>
public sealed class FormulaParametersResolveRuleTests
{
    /// <summary>The text that ends every message about text read out of place.</summary>
    private const string TextBelongs = "text is only compared with text by =, <> or IN, or passed to YEARFRAC";

    /// <summary>
    /// The seed's margin model on YEARFRAC passes: its formulas read <c>@dayCount</c>, a text parameter limited to the conventions,
    /// only as YEARFRAC's argument.
    /// </summary>
    [Fact]
    public void SeedFormulasOnYearFrac_Pass()
    {
        Assert.Empty(FormulaParametersResolveRule.Evaluate(YearFracGraph()));
    }

    /// <summary>
    /// Numbers read where numbers belong, and text compared with text by =, &lt;&gt;, IN and NOT IN, inside CASE conditions too,
    /// pass; so do YEARFRAC with a convention literal or a parameter limited to conventions.
    /// </summary>
    /// <param name="formula">Calculated measure X's formula.</param>
    [Theory]
    [InlineData("[m.V] * @rate / YEARFRAC(@dayCount)")]
    [InlineData("[m.V] * @periodDays / @yearDays")]
    [InlineData("[m.V] / YEARFRAC('thirty_360_us')")]
    [InlineData("CASE WHEN @dayCount = 'actual_360' THEN [m.V] ELSE 0 END")]
    [InlineData("CASE WHEN 'actual_360' <> @dayCount AND @label = @basis THEN 1 END")]
    [InlineData("CASE WHEN @dayCount IN ('actual_360', 'actual_365') THEN 1 ELSE 0 END")]
    [InlineData("CASE WHEN @dayCount NOT IN ('actual_360', @label) THEN 1 END")]
    [InlineData("CASE WHEN @rate > 0 AND NOT (@label = 'none') THEN COALESCE(@rate, 0) END")]
    public void ValuesWhereTheirTypesBelong_Pass(string formula)
    {
        Assert.Empty(Messages(formula));
    }

    /// <summary>A name that is neither a context parameter nor one the model declares is reported once, however often it is read.</summary>
    [Fact]
    public void UndeclaredNames_AreReported()
    {
        Assert.Equal(
            ["Calculated measure 'X' reads @margin, which is no parameter of the model, @periodDays or @yearDays."],
            Messages("[m.V] * @margin + @margin"));
    }

    /// <summary>
    /// A number parameter or a context parameter compared with text, or read as a condition, is reported, because the engine would
    /// compare a number with text or read a number as true or false.
    /// </summary>
    /// <param name="formula">Calculated measure X's formula.</param>
    /// <param name="expected">The messages.</param>
    [Theory]
    [InlineData("CASE WHEN @rate = @label THEN 1 END", "Calculated measure 'X' compares @rate, a number, with text.|Calculated measure 'X' reads @label, which is text, where a number belongs; " + TextBelongs + ".")]
    [InlineData("CASE WHEN @periodDays THEN 1 END", "Calculated measure 'X' reads @periodDays, a number, where a condition belongs.")]
    [InlineData("CASE WHEN NOT @rate THEN 1 END", "Calculated measure 'X' reads @rate, a number, where a condition belongs.")]
    public void NumbersOutOfPlace_AreReported(string formula, string expected)
    {
        Assert.Equal(expected.Split('|'), Messages(formula));
    }

    /// <summary>
    /// Text in arithmetic, in an ordering comparison, as a condition, as a CASE result, beside a number in an IN, or under IS NULL is
    /// reported, because the engine compares text only by =, &lt;&gt; and IN with other text.
    /// </summary>
    /// <param name="formula">Calculated measure X's formula.</param>
    /// <param name="expected">The messages.</param>
    [Theory]
    [InlineData("[m.V] * @label", "Calculated measure 'X' reads @label, which is text, where a number belongs; " + TextBelongs + ".")]
    [InlineData("CASE WHEN @dayCount < 'b' THEN 1 END", "Calculated measure 'X' reads @dayCount, which is text, where a number belongs; " + TextBelongs + ".|Calculated measure 'X' reads 'b', which is text, where a number belongs; " + TextBelongs + ".")]
    [InlineData("CASE WHEN @label THEN 1 END", "Calculated measure 'X' reads @label, which is text, where a condition belongs; " + TextBelongs + ".")]
    [InlineData("CASE WHEN @rate > 0 THEN 'yes' END", "Calculated measure 'X' reads 'yes', which is text, where a number belongs; " + TextBelongs + ".")]
    [InlineData("CASE WHEN @dayCount IN ('actual_360', 1) THEN 1 END", "Calculated measure 'X' reads @dayCount, which is text, where a number belongs; " + TextBelongs + ".|Calculated measure 'X' reads 'actual_360', which is text, where a number belongs; " + TextBelongs + ".")]
    [InlineData("CASE WHEN @dayCount IS NULL THEN 1 END", "Calculated measure 'X' reads @dayCount, which is text, where a number belongs; " + TextBelongs + ".")]
    public void TextOutOfPlace_IsReported(string formula, string expected)
    {
        Assert.Equal(expected.Split('|'), Messages(formula));
    }

    /// <summary>
    /// YEARFRAC takes a day count convention: a convention in quotes, or a text parameter whose allowed values are all conventions.
    /// Other text, a text parameter that may hold any text or other values, and anything that is not text are reported.
    /// </summary>
    /// <param name="formula">Calculated measure X's formula.</param>
    /// <param name="expected">The message, or null when the formula passes.</param>
    [Theory]
    [InlineData("[m.V] / YEARFRAC('actual_actual')", null)]
    [InlineData("[m.V] / YEARFRAC(@dayCount)", null)]
    [InlineData("[m.V] / YEARFRAC('monthly')", "Calculated measure 'X' calls YEARFRAC with 'monthly', which is no day count convention.")]
    [InlineData("[m.V] / YEARFRAC(@label)", "Calculated measure 'X' calls YEARFRAC with @label, which may hold text that is no day count convention; its allowed values must all be conventions.")]
    [InlineData("[m.V] / YEARFRAC(@basis)", "Calculated measure 'X' calls YEARFRAC with @basis, which may hold text that is no day count convention; its allowed values must all be conventions.")]
    [InlineData("[m.V] / YEARFRAC(@rate)", "Calculated measure 'X' calls YEARFRAC with something other than a day count convention, which it takes in quotes, such as 'actual_360', or from a text parameter whose allowed values are conventions.")]
    [InlineData("[m.V] / YEARFRAC(@periodDays)", "Calculated measure 'X' calls YEARFRAC with something other than a day count convention, which it takes in quotes, such as 'actual_360', or from a text parameter whose allowed values are conventions.")]
    [InlineData("[m.V] / YEARFRAC([m.V])", "Calculated measure 'X' calls YEARFRAC with something other than a day count convention, which it takes in quotes, such as 'actual_360', or from a text parameter whose allowed values are conventions.")]
    public void YearFracArguments_AreDayCountConventions(string formula, string? expected)
    {
        Assert.Equal(expected is null ? [] : [expected], Messages(formula));
    }

    /// <summary>Attribution expressions are formulas the engine evaluates too, so they are checked alike.</summary>
    [Fact]
    public void AttributionExpressions_AreChecked()
    {
        var model = Model12() with { Attributions = [Attribution("V", "[m.V] * @label")] };

        Assert.Equal(
            ["The attribution of 'V' reads @label, which is text, where a number belongs; " + TextBelongs + "."],
            FormulaParametersResolveRule.Evaluate(model).Select(violation => violation.Message));
    }

    /// <summary>
    /// Model 40 on formula language 1.2 with variable V and parameters <c>dayCount</c>, limited to the conventions, <c>rate</c>, a
    /// number, <c>label</c>, any text, and <c>basis</c>, limited to texts that are no conventions.
    /// </summary>
    private static ModelGraph Model12(params IReadOnlyList<CalculatedMeasureRecord> measures)
    {
        return Graph([Variable(51, "V")], measures) with
        {
            Model = Model() with { FormulaLanguageVersion = FormulaLanguageVersions.Version12 },
            Parameters =
            [
                DayCount(),
                NumberParameter("rate", 0.05m),
                TextParameter("label", "none"),
                TextParameter("basis", "a") with { AllowedValues = ["a", "b"] }
            ]
        };
    }

    /// <summary>The rule's messages for calculated measure X with a formula.</summary>
    private static IEnumerable<string> Messages(string formula)
    {
        return FormulaParametersResolveRule.Evaluate(Model12(Calculated(61, "X", formula))).Select(violation => violation.Message);
    }
}
