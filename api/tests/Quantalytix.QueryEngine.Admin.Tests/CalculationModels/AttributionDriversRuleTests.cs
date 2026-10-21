using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>attribution_drivers</c>. The engine switches each driver's inputs between two contexts and reads the residual as what
/// the drivers leave unexplained, so the drivers must split the expression's inputs among them exactly; their keys name expanded
/// columns and their sort orders fix the sequential order, so both are distinct; and Shapley attribution evaluates every subset of
/// the drivers, so a metric has one to eight of them, its own or the default ones.
/// </summary>
public sealed class AttributionDriversRuleTests
{
    /// <summary>The seed's drivers split their expressions' inputs exactly.</summary>
    [Fact]
    public void SeedDrivers_SplitTheirExpressions()
    {
        Assert.Empty(AttributionDriversRule.Evaluate(MarginGraph()));
    }

    /// <summary>
    /// An input no driver switches and an input two drivers switch are each reported, as is a driver input the expression does not
    /// read, because the effects would no longer add up to the change less the residual.
    /// </summary>
    [Fact]
    public void DriversThatDoNotSplitTheInputs_AreReported()
    {
        var model = MarginGraphWith(Attribution(
            "YIELD", null,
            Driver("income", 1, "INTEREST_INCOME"), Driver("volume", 2, "AVERAGE_BALANCE", "INTEREST_INCOME"), Driver("expense", 3, "INTEREST_EXPENSE")));

        Assert.Equal(
            ["The attribution of 'YIELD' reads 'INTEREST_INCOME', which more than one driver switches: 'income', 'volume'.",
             "The attribution of 'YIELD' reads '@yearDays', which no driver switches.",
             "The attribution of 'YIELD' reads '@periodDays', which no driver switches.",
             "The attribution of 'YIELD' has drivers that switch 'INTEREST_EXPENSE', which its expression does not read."],
            AttributionDriversRule.Evaluate(model).Select(violation => violation.Message));
    }

    /// <summary>A repeated driver key and a repeated sort order are each reported once.</summary>
    [Fact]
    public void RepeatedKeysAndSortOrders_AreReported()
    {
        var model = MarginGraphWith(Attribution(
            "YIELD", null,
            Driver("income", 1, "INTEREST_INCOME"), Driver("income", 1, "AVERAGE_BALANCE"), Driver("days", 3, "@periodDays", "@yearDays")));

        Assert.Equal(
            ["The attribution of 'YIELD' has more than one driver 'income'.", "The attribution of 'YIELD' has more than one driver at sort order 1."],
            AttributionDriversRule.Evaluate(model).Select(violation => violation.Message));
    }

    /// <summary>
    /// Without drivers of its own, a metric has one default driver per metric it reads and one days driver for both context
    /// parameters: nine metrics are too many and a constant has none, while @periodDays and @yearDays alone make one.
    /// </summary>
    /// <param name="expressionText">The attribution expression of variable <c>V1</c>.</param>
    /// <param name="defaultDrivers">The number of default drivers it has, or null when it passes.</param>
    [Theory]
    [InlineData("[m.V1] + [m.V2] + [m.V3] + [m.V4] + [m.V5] + [m.V6] + [m.V7] + [m.V8] + [m.V9]", 9)]
    [InlineData("1", 0)]
    [InlineData("[m.V1] + [m.V2] + [m.V3] + [m.V4] + [m.V5] + [m.V6] + [m.V7] * @yearDays / @periodDays", null)]
    public void DefaultDrivers_AreOneToEight(string expressionText, int? defaultDrivers)
    {
        var model = Graph([.. Enumerable.Range(1, 9).Select(index => Variable(index, $"V{index}"))], [], Attribution("V1", expressionText));

        var violations = AttributionDriversRule.Evaluate(model).Select(violation => violation.Message);

        if (defaultDrivers is null) Assert.Empty(violations);
        else Assert.Equal([$"The attribution of 'V1' has {defaultDrivers} default drivers, one per metric its expression reads and one for its days; a metric has 1 to 8."], violations);
    }

    /// <summary>Nine drivers of its own are too many, even when they split the expression exactly.</summary>
    [Fact]
    public void NineDrivers_AreReported()
    {
        var model = Graph(
            [.. Enumerable.Range(1, 9).Select(index => Variable(index, $"V{index}"))],
            [],
            Attribution(
                "V1", "[m.V1] + [m.V2] + [m.V3] + [m.V4] + [m.V5] + [m.V6] + [m.V7] + [m.V8] + [m.V9]",
                [.. Enumerable.Range(1, 9).Select(index => Driver($"d{index}", index, $"V{index}"))]));

        Assert.Equal(
            ["The attribution of 'V1' has 9 drivers; a metric has at most 8."],
            AttributionDriversRule.Evaluate(model).Select(violation => violation.Message));
    }
}
