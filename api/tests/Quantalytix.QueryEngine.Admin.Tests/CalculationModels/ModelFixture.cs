using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Reports;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// The model module over the report rules' seeded tables, wired as the API registers it: enabled report <c>balance_sheet</c> reads
/// model 40, whose variable <c>balance</c> it shows, scopes and fills, and whose calculated measure <c>yield</c> reads
/// <c>[m.balance] / 2</c>.
/// </summary>
internal sealed class ModelFixture
{
    /// <summary>The first id the generator allocates.</summary>
    public const long FirstNewId = 1000;

    /// <summary>The seeded tables.</summary>
    public ReportGraphFixture Tables { get; } = new();

    /// <summary>The tenant's semantic revision.</summary>
    public long Revision => Tables.DatabaseInfo.All.Single().SemanticRevision;

    /// <summary>The snapshot loader over the tables.</summary>
    /// <returns>The loader.</returns>
    public ModelSnapshotLoader Loader()
    {
        return new ModelSnapshotLoader(
            Tables.Models, Tables.Variables, Tables.CalculatedMeasures, Tables.AttributionService(), Tables.ParameterService(), Tables.Reports, Tables.Reader());
    }

    /// <summary>The model change-set pipeline with the gate and the findings, as the API registers them.</summary>
    /// <returns>The pipeline.</returns>
    public ChangeSetPipeline<ModelSnapshot, ModelOperation, ModelDocument> Pipeline()
    {
        return new ChangeSetPipeline<ModelSnapshot, ModelOperation, ModelDocument>(
            new ModelChanges(Loader(), Tables.Models, Tables.Variables, Tables.CalculatedMeasures, Tables.Parameters, Tables.Attributions, Tables.AttributionDrivers),
            [new ModelGateRule(), new ModelFindingsRule()],
            [new ModelOperationValidator()],
            new SequentialIdGenerator(FirstNewId),
            new SemanticRevision(Tables.DatabaseInfo, new FakeTimeProvider()));
    }

    /// <summary>The lifecycle commands over the tables.</summary>
    /// <returns>The commands.</returns>
    public ModelCommands Commands()
    {
        return new ModelCommands(
            Tables.Models, Tables.Variables, Tables.CalculatedMeasures, Tables.Parameters, Tables.Attributions, Tables.AttributionDrivers, Loader(),
            new SequentialIdGenerator(FirstNewId));
    }

    /// <summary>The reads over the tables.</summary>
    /// <returns>The queries.</returns>
    public ModelQueries Queries()
    {
        return new ModelQueries(Tables.Models, Tables.Variables, Tables.CalculatedMeasures, Tables.Reports, Loader());
    }

    /// <summary>Disables the seeded report, so the engine no longer reads the model through it.</summary>
    /// <returns>A task that completes when the report is updated.</returns>
    public Task DisableReportAsync()
    {
        return Tables.Reports.UpdateAsync(Tables.Reports.All.Single() with { IsEnabled = false }, CancellationToken.None);
    }

    /// <summary>A change set of the operations.</summary>
    /// <param name="operations">The operations, in order.</param>
    /// <returns>The request.</returns>
    public static ChangeSetRequest<ModelOperation> Request(params IReadOnlyList<ModelOperation> operations)
    {
        return new ChangeSetRequest<ModelOperation> { Operations = operations };
    }

    /// <summary>A currency variable that reads missing values as zero.</summary>
    /// <param name="key">Its key.</param>
    /// <returns>The variable.</returns>
    public static ModelVariable Variable(string key)
    {
        return new ModelVariable { Key = key, DisplayName = key, DisplayFormat = "currency", UnitKey = null, MissingBehavior = "zero" };
    }

    /// <summary>A decimal calculated measure.</summary>
    /// <param name="key">Its key.</param>
    /// <param name="formula">Its formula.</param>
    /// <returns>The calculated measure.</returns>
    public static ModelMeasure Measure(string key, string formula)
    {
        return new ModelMeasure
        {
            Key = key,
            DisplayName = key,
            Formula = formula,
            OutputDataType = "decimal",
            OutputPrecision = 20,
            OutputScale = 4,
            RoundingMode = null,
            UnitKey = null,
            DisplayFormat = "percent"
        };
    }

    /// <summary>The seed's day count parameter as the contract carries it, with the given allowed values.</summary>
    /// <param name="allowedValues">The conventions it may take.</param>
    /// <returns>The parameter.</returns>
    public static ModelParameterItem DayCount(params IReadOnlyList<string> allowedValues)
    {
        return new ModelParameterItem
        {
            Key = "dayCount",
            DisplayName = "Day count",
            ValueType = "text",
            AllowedValues = allowedValues,
            DefaultNumber = null,
            DefaultText = allowedValues[0],
            Description = null
        };
    }
}
