using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Measures;

/// <summary>
/// Confirms that <c>formula_acyclic</c> reports each group of formula measures that depend on each other once, naming its
/// measures, and a measure that depends on itself, while a dependency graph without cycles passes and a measure that only
/// depends on a cycle is not blamed for it. The query engine evaluates a formula after the measures it depends on, so a
/// cycle can never be evaluated (schema sections 13 and 18).
/// </summary>
public sealed class FormulaAcyclicRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The version's formula measures a, b and c, with IDs 1, 2 and 3.</summary>
    private readonly InMemoryTableGateway<MeasureRecord> _measures = new(
        Formula(1, "a"),
        Formula(2, "b"),
        Formula(3, "c"));

    /// <summary>Dependencies without a cycle pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DependenciesWithoutACycle_Pass()
    {
        Assert.Empty(await EvaluateAsync(Edge(1, 2), Edge(1, 3), Edge(2, 3)));
    }

    /// <summary>Two measures that depend on each other are reported once, naming both.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MutualDependency_IsReportedOnce()
    {
        var violation = Assert.Single(await EvaluateAsync(Edge(1, 2), Edge(2, 1)));

        Assert.Equal(FormulaAcyclicRule.Code, violation.Rule);
        Assert.Equal("Formula measures 'a', 'b' depend on each other in a cycle.", violation.Message);
    }

    /// <summary>A measure that depends on itself is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SelfReference_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(Edge(3, 3)));

        Assert.Equal("Formula measure 'c' references itself.", violation.Message);
    }

    /// <summary>A measure that depends on a cycle without being on it is not named.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MeasureDependingOnACycle_IsNotNamed()
    {
        var violation = Assert.Single(await EvaluateAsync(Edge(3, 1), Edge(1, 2), Edge(2, 1)));

        Assert.DoesNotContain("'c'", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>Evaluates the rule over the given dependency rows.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params MeasureDependencyRecord[] edges)
    {
        var rule = new FormulaAcyclicRule(_measures, new InMemoryTableGateway<MeasureDependencyRecord>(edges));
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>A formula measure of the version; the rule reads the dependency rows, not the expression.</summary>
    private static MeasureRecord Formula(long measureId, string measureKey)
    {
        return DatasetRows.Measure(measureId, VersionId, measureKey, MeasureKinds.Formula) with { ExpressionText = "1" };
    }

    /// <summary>A dependency of one measure on another.</summary>
    private static MeasureDependencyRecord Edge(long measureId, long dependencyMeasureId)
    {
        return new MeasureDependencyRecord { MeasureId = measureId, DependencyMeasureId = dependencyMeasureId };
    }
}
