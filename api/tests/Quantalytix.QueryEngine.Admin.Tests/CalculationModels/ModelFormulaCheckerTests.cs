using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>formula-check</c> (OI-43), which the formula editor calls while the user types instead of deciding in the browser: a
/// syntax error comes from the query engine's parser at the character it names; a reference the model lacks, a calculated measure
/// reading itself and an undeclared parameter cover their own text; a formula is told the oldest language that has what it uses, and
/// which metrics and declared parameters it reads.
/// </summary>
public sealed class ModelFormulaCheckerTests
{
    /// <summary>A syntax error is the parser's, at the character it names.</summary>
    [Fact]
    public void SyntaxError_IsAtTheParsersPosition()
    {
        var check = ModelFormulaChecker.Check(MarginGraph(), "[m.YIELD] * * 2", "YIELD");

        var error = Assert.Single(check.Errors);
        Assert.Equal((12, 13), (error.Start, error.End));
        Assert.Empty(check.Reads);
    }

    /// <summary>An unknown metric covers each of its references, and the reads list every metric in order of first use.</summary>
    [Fact]
    public void UnknownMetric_CoversItsReferences()
    {
        var check = ModelFormulaChecker.Check(MarginGraph(), "[m.RATE] * [m.AVERAGE_BALANCE] / [m.RATE]", null);

        Assert.Equal([(0, 8), (33, 41)], check.Errors.Select(error => (error.Start, error.End)));
        Assert.Equal(["RATE", "AVERAGE_BALANCE"], check.Reads);
    }

    /// <summary>
    /// A calculated measure cannot read itself, directly or through the saved formulas of other measures; a variable's attribution
    /// expression may read anything, so the seed's INTEREST_INCOME identity reading YIELD, which reads it, passes.
    /// </summary>
    [Fact]
    public void SelfReads_AreReportedForCalculatedMeasuresOnly()
    {
        var model = MarginGraph() with { CalculatedMeasures = [.. MarginGraph().CalculatedMeasures, Calculated(62, "DOUBLE", "[m.YIELD] * 2")] };

        var measure = ModelFormulaChecker.Check(model, "[m.DOUBLE] + 1", "YIELD");
        var attribution = ModelFormulaChecker.Check(model, "[m.AVERAGE_BALANCE] * [m.YIELD]", "INTEREST_INCOME");

        Assert.Equal(["[m.DOUBLE] reads this calculated measure, so the formula would read itself."], measure.Errors.Select(error => error.Message));
        Assert.Empty(attribution.Errors);
    }

    /// <summary>
    /// On language 1.2, a declared parameter is a read and needs 1.2; an undeclared one covers its own text and is not a read; the
    /// context parameters are neither.
    /// </summary>
    [Fact]
    public void Parameters_AreReadsWhenDeclared()
    {
        const string formula = "[m.INTEREST_INCOME] / YEARFRAC(@dayCount) * @rate / @periodDays";

        var check = ModelFormulaChecker.Check(YearFracGraph(), formula, null);

        Assert.Equal(["dayCount"], check.Parameters);
        Assert.Equal(FormulaLanguageVersions.Version12, check.RequiresLanguage);
        var error = Assert.Single(check.Errors);
        Assert.Equal((formula.IndexOf("@rate", StringComparison.Ordinal), formula.IndexOf("@rate", StringComparison.Ordinal) + 5), (error.Start, error.End));
    }

    /// <summary>A formula that uses what the model's language lacks is told so, and the language it needs.</summary>
    [Fact]
    public void ALanguageTheModelLacks_IsReported()
    {
        var check = ModelFormulaChecker.Check(MarginGraph(), "POWER([m.YIELD], 2)", null);

        Assert.Equal(FormulaLanguageVersions.Version11, check.RequiresLanguage);
        Assert.Equal(["The formula calls POWER, which formula language 1.0 lacks; the model needs formula language 1.1."], check.Errors.Select(error => error.Message));
    }
}
