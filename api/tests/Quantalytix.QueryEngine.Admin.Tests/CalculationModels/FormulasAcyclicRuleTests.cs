using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>formulas_acyclic</c>: the calculated measures on a cycle are each reported, while a measure that only reads a
/// measure on the cycle, and formulas over variables, are not. The report orchestrator evaluates each formula after the ones
/// it reads, so a cycle has no evaluation order; a measure outside the cycle is reported through the measures it reads.
/// </summary>
public sealed class FormulasAcyclicRuleTests
{
    /// <summary>Two measures that read each other are reported; the measure that reads one of them and the variable formula are not.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MeasuresOnACycle_AreReported()
    {
        var measures = new InMemoryTableGateway<CalculatedMeasureRecord>(
            Calculated(91, "a", "[m.b] + 1"),
            Calculated(92, "b", "[m.a] * 2"),
            Calculated(93, "c", "[m.a] / 2"),
            Calculated(94, "yield", "[m.income] / [m.balance]"));

        var violations = await new FormulasAcyclicRule(measures).EvaluateAsync(ModelVersionId, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Calculated measure 'a' reads itself through the calculated measures its formula reads.",
             "Calculated measure 'b' reads itself through the calculated measures its formula reads."],
            violations.Select(violation => violation.Message));
    }
}
