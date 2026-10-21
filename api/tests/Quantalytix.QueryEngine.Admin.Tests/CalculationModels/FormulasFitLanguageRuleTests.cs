using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>formulas_fit_language</c>. The engine evaluates a model's formulas and attribution expressions in the model's formula
/// language, and only language 1.1 has POWER, so a model on 1.0 may not call it anywhere; on 1.1 it may.
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

    /// <summary>On language 1.1, the same formulas pass.</summary>
    [Fact]
    public void PowerOnLanguage11_Passes()
    {
        Assert.Empty(FormulasFitLanguageRule.Evaluate(Compounding(FormulaLanguageVersions.Version11)));
    }
}
