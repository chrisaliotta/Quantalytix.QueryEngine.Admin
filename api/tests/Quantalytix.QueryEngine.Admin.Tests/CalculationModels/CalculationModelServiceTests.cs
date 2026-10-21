using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms how a calculation model is deleted. The model owns its variables and calculated measures, so deleting it removes them
/// and leaves other models' metrics alone. A report names its model whether it is enabled or not, so a model any report reads is
/// not deleted, and the refusal names the reports, which the administrator must delete or repoint first.
/// </summary>
public sealed class CalculationModelServiceTests
{
    /// <summary>Model 40.</summary>
    private readonly InMemoryTableGateway<CalculationModelRecord> _models = new(Model());

    /// <summary>Model 40's balance variable and model 41's rate variable.</summary>
    private readonly InMemoryTableGateway<VariableRecord> _variables = new(Variable(51, "balance"), Variable(52, "rate") with { ModelId = 41 });

    /// <summary>Model 40's yield measure.</summary>
    private readonly InMemoryTableGateway<CalculatedMeasureRecord> _measures = new(Calculated(61, "yield", "[m.balance] / 2"));

    /// <summary>A model no report reads is deleted with its own metrics; model 41's variable stays.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task ModelNoReportReads_IsDeletedWithItsMetrics()
    {
        var conflict = await Service(new InMemoryTableGateway<ReportRecord>()).DeleteAsync(ModelId, TestContext.Current.CancellationToken);

        Assert.Null(conflict);
        Assert.Empty(_models.All);
        Assert.Equal(["rate"], _variables.All.Select(variable => variable.VariableKey));
        Assert.Empty(_measures.All);
    }

    /// <summary>A model a disabled report reads is not deleted: the refusal names the report, and the model and its metrics stay.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task ModelAReportReads_IsNotDeleted()
    {
        var reports = new InMemoryTableGateway<ReportRecord>(Report() with { IsEnabled = false });

        var conflict = await Service(reports).DeleteAsync(ModelId, TestContext.Current.CancellationToken);

        Assert.Equal("Calculation model 'margin' cannot be deleted: read by report 'balance_sheet'.", conflict);
        Assert.Single(_models.All);
        Assert.Equal(2, _variables.All.Count);
        Assert.Single(_measures.All);
    }

    /// <summary>The service over this test's tables and the given reports.</summary>
    private CalculationModelService Service(InMemoryTableGateway<ReportRecord> reports)
    {
        return new CalculationModelService(_models, _variables, _measures, new ReportUses(reports), new SequentialIdGenerator(1000));
    }
}
