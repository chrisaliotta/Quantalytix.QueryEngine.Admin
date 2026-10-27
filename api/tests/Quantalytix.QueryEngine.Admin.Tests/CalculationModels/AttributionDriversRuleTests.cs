using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>attribution_drivers</c>. The engine switches each driver's inputs between two contexts and reads the residual as what
/// the drivers leave unexplained, so the drivers must split the expression's inputs among them exactly; their keys name expanded
/// columns, so they are distinct (their order is the sequential order, so it cannot repeat); and Shapley attribution evaluates every
/// subset of the drivers, so a metric has one to eight of them, its own or the default ones.
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
            Driver("income", "INTEREST_INCOME"), Driver("volume", "AVERAGE_BALANCE", "INTEREST_INCOME"), Driver("expense", "INTEREST_EXPENSE")));

        Assert.Equal(
            ["The attribution of 'YIELD' reads 'INTEREST_INCOME', which more than one driver switches: 'income', 'volume'.",
             "The attribution of 'YIELD' reads '@yearDays', which no driver switches.",
             "The attribution of 'YIELD' reads '@periodDays', which no driver switches.",
             "The attribution of 'YIELD' has drivers that switch 'INTEREST_EXPENSE', which its expression does not read."],
            AttributionDriversRule.Evaluate(model).Select(violation => violation.Message));
    }

    /// <summary>A repeated driver key is reported once, with the attribution as its target.</summary>
    [Fact]
    public void RepeatedKeys_AreReported()
    {
        var model = MarginGraphWith(Attribution(
            "YIELD", null,
            Driver("income", "INTEREST_INCOME"), Driver("income", "AVERAGE_BALANCE"), Driver("days", "@periodDays", "@yearDays")));

        var violation = Assert.Single(AttributionDriversRule.Evaluate(model));
        Assert.Equal("The attribution of 'YIELD' has more than one driver 'income'.", violation.Message);
        Assert.Equal(ModelAreas.Attribution("YIELD"), violation.Target);
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

    /// <summary>
    /// YEARFRAC reads the period, so a formula that calls it reads both days inputs: the seed's YIELD and INTEREST_INCOME days drivers,
    /// which switch @periodDays and @yearDays, stay valid once their formulas annualize by YEARFRAC(@dayCount).
    /// </summary>
    [Fact]
    public void SeedDaysDrivers_StayValidOnYearFrac()
    {
        Assert.Equal(
            ["INTEREST_INCOME", "AVERAGE_BALANCE", "@periodDays", "@yearDays"],
            AttributionInputs.Of("[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)"));
        Assert.Empty(AttributionDriversRule.Evaluate(YearFracGraph()));
        Assert.Empty(AttributionReferencesResolveRule.Evaluate(YearFracGraph()));
    }

    /// <summary>
    /// The days are one input pair: reading either context parameter, or calling YEARFRAC, reads both, the ones read first in reading
    /// order; a declared parameter is never an input, because a row's value is the same on both sides of a comparison.
    /// </summary>
    /// <param name="expressionText">An attribution expression.</param>
    /// <param name="inputs">Its inputs.</param>
    [Theory]
    [InlineData("[m.V1] * @periodDays", new[] { "V1", "@periodDays", "@yearDays" })]
    [InlineData("[m.V1] * @yearDays / @periodDays", new[] { "V1", "@yearDays", "@periodDays" })]
    [InlineData("[m.V1] * YEARFRAC('actual_360')", new[] { "V1", "@periodDays", "@yearDays" })]
    [InlineData("[m.V1] * @rate", new[] { "V1" })]
    public void DaysInputs_ComeAsAPair(string expressionText, string[] inputs)
    {
        Assert.Equal(inputs, AttributionInputs.Of(expressionText));
    }

    /// <summary>
    /// The default drivers are the engine's: one per metric read, keyed and named as the metric, then one <c>days</c> driver that
    /// switches both context parameters, which a formula that annualizes by YEARFRAC alone has too.
    /// </summary>
    [Fact]
    public void DefaultDrivers_AreKeyedAsTheEngineKeysThem()
    {
        var model = Graph([Variable(1, "A") with { DisplayName = "Income" }, Variable(2, "B")], []);

        var drivers = AttributionInputs.DefaultDrivers(model, "[m.A] / [m.B] / YEARFRAC(@dayCount)");

        Assert.Equal(["A", "B", "days"], drivers.Select(driver => driver.Key));
        Assert.Equal(["Income", "B", "Days"], drivers.Select(driver => driver.DisplayName));
        Assert.Equal(["@periodDays", "@yearDays"], drivers[2].Inputs);
    }

    /// <summary>
    /// A driver that switches a declared parameter is reported by both attribution rules: the expression has no such input, and an
    /// input is a metric or a context parameter.
    /// </summary>
    [Fact]
    public void DriversOfDeclaredParameters_AreReported()
    {
        var model = YearFracGraph();
        var yield = model.Attributions[0];
        var withBasis = model with { Attributions = [yield with { Drivers = [.. yield.Drivers, Driver("basis", "@dayCount")] }, model.Attributions[1]] };

        Assert.Equal(
            ["The attribution of 'YIELD' has drivers that switch '@dayCount', which its expression does not read."],
            AttributionDriversRule.Evaluate(withBasis).Select(violation => violation.Message));
        Assert.Equal(
            ["The attribution of 'YIELD' has driver 'basis' switch '@dayCount', which is no variable or calculated measure of the model, @periodDays or @yearDays."],
            AttributionReferencesResolveRule.Evaluate(withBasis).Select(violation => violation.Message));
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
                [.. Enumerable.Range(1, 9).Select(index => Driver($"d{index}", $"V{index}"))]));

        Assert.Equal(
            ["The attribution of 'V1' has 9 drivers; a metric has at most 8."],
            AttributionDriversRule.Evaluate(model).Select(violation => violation.Message));
    }
}
