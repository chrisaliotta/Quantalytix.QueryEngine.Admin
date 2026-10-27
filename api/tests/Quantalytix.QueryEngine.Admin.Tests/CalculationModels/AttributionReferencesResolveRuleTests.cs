using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>attribution_references_resolve</c>. The engine evaluates an attribution expression against the model's keys alone and
/// switches each driver's inputs between two contexts, so every key an attribution names must be a metric of the model, the context
/// parameters being the only other inputs, and a variable, which has no formula, must bring its own expression.
/// </summary>
public sealed class AttributionReferencesResolveRuleTests
{
    /// <summary>The seed's attributions resolve, including drivers that switch @periodDays and @yearDays.</summary>
    [Fact]
    public void SeedAttributions_Resolve()
    {
        Assert.Empty(AttributionReferencesResolveRule.Evaluate(MarginGraph()));
    }

    /// <summary>An attribution of a metric the model lacks is reported, because the engine would attribute nothing.</summary>
    [Fact]
    public void AttributionOfAMissingMetric_IsReported()
    {
        var model = MarginGraphWith(Attribution("NIM", "[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE]"));

        Assert.Equal(
            ["The attribution of 'NIM' names no variable or calculated measure of the model."],
            AttributionReferencesResolveRule.Evaluate(model).Select(violation => violation.Message));
    }

    /// <summary>A variable's attribution without an expression is reported: unlike a calculated measure, it has no formula to fall back on.</summary>
    [Fact]
    public void VariableAttributionWithoutAnExpression_IsReported()
    {
        var model = MarginGraphWith(Attribution("INTEREST_INCOME", null));

        Assert.Equal(
            ["The attribution of 'INTEREST_INCOME' needs an expression, because a variable has no formula to switch its drivers in."],
            AttributionReferencesResolveRule.Evaluate(model).Select(violation => violation.Message));
    }

    /// <summary>
    /// A key the model lacks is reported once in the expression and once per driver that switches it, through the same check formulas
    /// use; the context parameters a driver switches resolve.
    /// </summary>
    [Fact]
    public void KeysTheModelLacks_AreReported()
    {
        var model = MarginGraphWith(Attribution(
            "INTEREST_INCOME", "[m.AVERAGE_BALANCE] * [m.RATE] * @periodDays / @yearDays",
            Driver("volume", "AVERAGE_BALANCE"), Driver("rate", "RATE"), Driver("days", "@periodDays", "@yearDays")));

        Assert.Equal(
            ["The attribution of 'INTEREST_INCOME' reads [m.RATE], which is no variable or calculated measure of the model.",
             "The attribution of 'INTEREST_INCOME' has driver 'rate' switch 'RATE', which is no variable or calculated measure of the model, @periodDays or @yearDays."],
            AttributionReferencesResolveRule.Evaluate(model).Select(violation => violation.Message));
    }
}
