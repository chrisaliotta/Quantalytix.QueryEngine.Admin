using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms that cloning a model version copies its metrics, variables and calculated measures, under new IDs with their keys and
/// formulas as they are. They reference each other by key, so nothing needs remapping; a clone that kept the source's IDs would
/// change the published version when the draft is edited.
/// </summary>
public sealed class ModelVersionClonerTests
{
    /// <summary>The draft the metrics are cloned into.</summary>
    private const long TargetVersionId = 42;

    /// <summary>Version 41 has one variable and one calculated measure that reads it.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Metrics_AreCopiedUnderNewIds()
    {
        var variables = new InMemoryTableGateway<VariableRecord>(Variable(51, "balance"));
        var measures = new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(91, "yield", "[m.balance] * 2"));

        await new ModelVersionCloner(variables, measures, new SequentialIdGenerator(1000)).CloneAsync(ModelVersionId, TargetVersionId, TestContext.Current.CancellationToken);

        var variable = Assert.Single(variables.All, row => row.ModelVersionId == TargetVersionId);
        var measure = Assert.Single(measures.All, row => row.ModelVersionId == TargetVersionId);
        Assert.NotEqual(51, variable.VariableId);
        Assert.NotEqual(91, measure.CalculatedMeasureId);
        Assert.Equal(Variable(variable.VariableId, "balance") with { ModelVersionId = TargetVersionId }, variable);
        Assert.Equal(("yield", "[m.balance] * 2"), (measure.MeasureKey, measure.ExpressionText));
    }
}
