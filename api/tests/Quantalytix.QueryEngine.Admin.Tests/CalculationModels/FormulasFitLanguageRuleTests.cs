using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>formulas_fit_language</c>. The engine evaluates a model's formulas and attribution expressions in the model's formula
/// language: only language 1.1 and later have POWER, and only 1.2 has YEARFRAC, string literals and the model's declared parameters,
/// so a model on an earlier language may not use them anywhere; <c>@periodDays</c> and <c>@yearDays</c> are in every language.
/// </summary>
public sealed class FormulasFitLanguageRuleTests
{
    /// <summary>A model whose formula and attribution expression call POWER.</summary>
    private static ModelGraph Compounding(string formulaLanguageVersion)
    {
        var model = MarginGraph();
        return MetricAttributionService.Put(
            model with
            {
                Model = model.Model with { FormulaLanguageVersion = formulaLanguageVersion },
                CalculatedMeasures = [.. model.CalculatedMeasures, Calculated(62, "GROWTH", "POWER([m.YIELD] + 1, @yearDays / @periodDays) - 1")]
            },
            Attribution("INTEREST_INCOME", "[m.AVERAGE_BALANCE] * POWER([m.YIELD], 1) * @periodDays / @yearDays"));
    }

    /// <summary>On language 1.0, the formula and the attribution expression that call POWER are each reported.</summary>
    [Fact]
    public void PowerOnLanguage10_IsReported()
    {
        Assert.Equal(
            ["Calculated measure 'GROWTH' calls POWER, which formula language 1.0 lacks; the model needs formula language 1.1.",
             "The attribution of 'INTEREST_INCOME' calls POWER, which formula language 1.0 lacks; the model needs formula language 1.1."],
            FormulasFitLanguageRule.Evaluate(Compounding(FormulaLanguageVersions.Version10)).Select(violation => violation.Message));
    }

    /// <summary>On language 1.1, and on 1.2, which has everything 1.1 has, the same formulas pass.</summary>
    /// <param name="formulaLanguageVersion">The model's language.</param>
    [Theory]
    [InlineData(FormulaLanguageVersions.Version11)]
    [InlineData(FormulaLanguageVersions.Version12)]
    public void PowerOnLanguage11AndLater_Passes(string formulaLanguageVersion)
    {
        Assert.Empty(FormulasFitLanguageRule.Evaluate(Compounding(formulaLanguageVersion)));
    }

    /// <summary>
    /// A formula that calls YEARFRAC, compares string literals and reads a declared parameter is reported once for each on languages
    /// 1.0 and 1.1, and passes on 1.2.
    /// </summary>
    /// <param name="formulaLanguageVersion">The model's language.</param>
    /// <param name="fits">Whether the language has all three.</param>
    [Theory]
    [InlineData(FormulaLanguageVersions.Version10, false)]
    [InlineData(FormulaLanguageVersions.Version11, false)]
    [InlineData(FormulaLanguageVersions.Version12, true)]
    public void YearFracTextAndDeclaredParameters_NeedLanguage12(string formulaLanguageVersion, bool fits)
    {
        var model = Graph([Variable(51, "V")], [Calculated(61, "X", "CASE WHEN @dayCount = 'actual_360' THEN [m.V] / YEARFRAC(@dayCount) END")]) with
        {
            Model = Model() with { FormulaLanguageVersion = formulaLanguageVersion },
            Parameters = [DayCount()]
        };

        var messages = FormulasFitLanguageRule.Evaluate(model).Select(violation => violation.Message);

        if (fits) Assert.Empty(messages);
        else
        {
            Assert.Equal(
                [$"Calculated measure 'X' calls YEARFRAC, which formula language {formulaLanguageVersion} lacks; the model needs formula language 1.2.",
                 $"Calculated measure 'X' reads string literals, which formula language {formulaLanguageVersion} lacks; the model needs formula language 1.2.",
                 $"Calculated measure 'X' reads parameter @dayCount, which formula language {formulaLanguageVersion} lacks; the model needs formula language 1.2."],
                messages);
        }
    }

    /// <summary>
    /// The context parameters are in every language, so the seed's language 1.0 formulas pass; a name the model does not declare is
    /// <c>formula_parameters_resolve</c>'s to report, not a language feature.
    /// </summary>
    [Fact]
    public void ContextAndUndeclaredParameters_FitEveryLanguage()
    {
        var model = MarginGraph() with { CalculatedMeasures = [Calculated(61, "YIELD", "[m.INTEREST_INCOME] * @yearDays / @periodDays * @margin")] };

        Assert.Equal(FormulaLanguageVersions.Version10, model.Model.FormulaLanguageVersion);
        Assert.Empty(FormulasFitLanguageRule.Evaluate(model));
    }
}
