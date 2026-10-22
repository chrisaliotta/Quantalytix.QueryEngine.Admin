using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms how a calculation model is deleted. The model owns its variables, calculated measures, their attributions and its
/// parameters, so deleting it removes them and leaves other models' rows alone. A report names its model whether it is enabled or
/// not, so a model any report reads is not deleted, and the refusal names the reports, which the administrator must delete or
/// repoint first.
/// </summary>
public sealed class CalculationModelServiceTests
{
    /// <summary>Model 40.</summary>
    private readonly InMemoryTableGateway<CalculationModelRecord> _models = new(Model());

    /// <summary>Model 40's balance variable and model 41's rate variable.</summary>
    private readonly InMemoryTableGateway<VariableRecord> _variables = new(Variable(51, "balance"), Variable(52, "rate") with { ModelId = 41 });

    /// <summary>Model 40's yield measure.</summary>
    private readonly InMemoryTableGateway<CalculatedMeasureRecord> _measures = new(Calculated(61, "yield", "[m.balance] / 2"));

    /// <summary>The attributions of model 40's yield and model 41's rate.</summary>
    private readonly InMemoryTableGateway<MetricAttributionRecord> _attributions = new(
        new MetricAttributionRecord { ModelId = ModelId, MetricKey = "yield" },
        new MetricAttributionRecord { ModelId = 41, MetricKey = "rate", ExpressionText = "[m.rate]" });

    /// <summary>The one driver of model 40's yield.</summary>
    private readonly InMemoryTableGateway<MetricAttributionDriverRecord> _drivers = new(new MetricAttributionDriverRecord
    {
        ModelId = ModelId, MetricKey = "yield", DriverKey = "balance", DisplayName = "Balance", SortOrder = 1, InputKeysJson = """["balance"]"""
    });

    /// <summary>Model 40's day count parameter and model 41's scale parameter.</summary>
    private readonly InMemoryTableGateway<ModelParameterRecord> _parameters = new(
        new ModelParameterRecord { ModelId = ModelId, ParameterKey = "dayCount", DisplayName = "Day count", ValueType = "text", DefaultText = "actual_actual" },
        new ModelParameterRecord { ModelId = 41, ParameterKey = "scale", DisplayName = "Scale", ValueType = "number", DefaultNumber = 1m });

    /// <summary>
    /// A model no report reads is deleted with its own metrics, attributions and parameters; model 41's variable, attribution and
    /// parameter stay.
    /// </summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task ModelNoReportReads_IsDeletedWithItsMetrics()
    {
        var conflict = await Service(new InMemoryTableGateway<ReportRecord>()).DeleteAsync(ModelId, TestContext.Current.CancellationToken);

        Assert.Null(conflict);
        Assert.Empty(_models.All);
        Assert.Equal(["rate"], _variables.All.Select(variable => variable.VariableKey));
        Assert.Empty(_measures.All);
        Assert.Equal(["rate"], _attributions.All.Select(attribution => attribution.MetricKey));
        Assert.Empty(_drivers.All);
        Assert.Equal(["scale"], _parameters.All.Select(parameter => parameter.ParameterKey));
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
        return new CalculationModelService(_models, _variables, _measures, _attributions, _drivers, _parameters, new ReportUses(reports), new SequentialIdGenerator(1000));
    }
}
