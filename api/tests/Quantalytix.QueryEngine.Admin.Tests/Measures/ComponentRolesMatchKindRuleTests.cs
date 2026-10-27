using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Measures;

/// <summary>
/// Confirms that <c>component_roles_match_kind</c> judges an aggregate's components by its aggregation function: a count
/// without a value component publishes, because the query engine counts rows for it, while a sum without one is reported.
/// The seed kit's dataset versions carry such row counts, so publication must accept what the engine runs.
/// </summary>
public sealed class ComponentRolesMatchKindRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>A row count passes while a sum without a value component is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RowCountPasses_WhileASumWithoutAValueIsReported()
    {
        var measures = new InMemoryTableGateway<MeasureRecord>(
            Aggregate(1, "rowCount", AggregationFunctions.Count),
            Aggregate(2, "balance", AggregationFunctions.Sum));
        var rule = new ComponentRolesMatchKindRule(measures, new InMemoryTableGateway<MeasureComponentRecord>());

        var violation = Assert.Single(await rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken));

        Assert.Equal(ComponentRolesMatchKindRule.Code, violation.Code);
        Assert.Equal("Measure 'balance': 'aggregate' measures need a 'value' component.", violation.Message);
    }

    /// <summary>An aggregate measure of the version with no components.</summary>
    private static MeasureRecord Aggregate(long measureId, string measureKey, string aggregationFunction)
    {
        return DatasetRows.Measure(measureId, VersionId, measureKey, MeasureKinds.Aggregate) with { AggregationFunction = aggregationFunction };
    }
}
