using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>attribution_weight_variable</c>. The engine weighs each member's rate by the member's value of the weight variable
/// when it splits a ratio's change into rate, mix and interaction, so the weight must be a variable: a calculated measure, which is
/// itself a ratio, or a key the model lacks, is reported.
/// </summary>
public sealed class AttributionWeightVariableRuleTests
{
    /// <summary>A variable weighs members; a calculated measure or a missing key does not.</summary>
    /// <param name="weightVariableKey">The weight of YIELD's attribution.</param>
    /// <param name="passes">Whether the rule passes.</param>
    [Theory]
    [InlineData("AVERAGE_BALANCE", true)]
    [InlineData("YIELD", false)]
    [InlineData("LOAN_BALANCE", false)]
    public void WeightVariable_IsAVariableOfTheModel(string weightVariableKey, bool passes)
    {
        var yield = MarginGraph().Attributions.Single(attribution => attribution.MetricKey == "YIELD");

        var violations = AttributionWeightVariableRule.Evaluate(MarginGraphWith(yield with { WeightVariableKey = weightVariableKey }));

        if (passes) Assert.Empty(violations);
        else Assert.Equal([$"The attribution of 'YIELD' weighs members by '{weightVariableKey}', which is no variable of the model."], violations.Select(violation => violation.Message));
    }
}
