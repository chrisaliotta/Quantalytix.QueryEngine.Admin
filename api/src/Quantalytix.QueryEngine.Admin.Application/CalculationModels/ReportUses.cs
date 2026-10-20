using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Names the report rows that use a model. Report versions of any status name the model itself. Published report versions name
/// its content by key, resolved in the model's published version: columns return variables and calculated measures, formula
/// lines evaluate calculated measures, and versions default to a profile. Deleting such content from the published version
/// would leave them unresolved; a draft's content is used by no report.
/// </summary>
public sealed class ReportUses(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<CalculationModelVersionRecord> modelVersions)
{
    /// <summary>The report versions of any status that read a model.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report version, like <c>'bank.margin' v2</c>.</returns>
    public async Task<IEnumerable<string>> ModelReadersAsync(long modelId, CancellationToken cancellationToken)
    {
        var readers = await NamedAsync(await versions.ListAsync(new { ModelId = modelId }, cancellationToken), cancellationToken);
        return readers.Values;
    }

    /// <summary>The columns of live reports that return a variable.</summary>
    /// <param name="variable">A variable of a model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per column.</returns>
    public async Task<IEnumerable<string>> VariableColumnsAsync(VariableRecord variable, CancellationToken cancellationToken)
    {
        var readers = await LiveReadersAsync(variable.ModelVersionId, cancellationToken);
        var returning = await columns.ListAsync(
            new { ReportVersionId = readers.Keys.ToArray(), ValueKind = ReportValueKinds.Variable, MetricKey = variable.VariableKey },
            cancellationToken);
        return returning.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportVersionId]}");
    }

    /// <summary>The columns and formula lines of live reports that read a calculated measure.</summary>
    /// <param name="measure">A calculated measure of a model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per column or line.</returns>
    public async Task<IEnumerable<string>> MeasureUsesAsync(CalculatedMeasureRecord measure, CancellationToken cancellationToken)
    {
        var readers = await LiveReadersAsync(measure.ModelVersionId, cancellationToken);
        long[] readerIds = [.. readers.Keys];
        var returning = await columns.ListAsync(
            new { ReportVersionId = readerIds, ValueKind = ReportValueKinds.Calculated, MetricKey = measure.MeasureKey },
            cancellationToken);
        var evaluating = await lines.ListAsync(new { ReportVersionId = readerIds, CalculatedMeasureKey = measure.MeasureKey }, cancellationToken);
        return
        [
            .. returning.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportVersionId]}"),
            .. evaluating.Select(line => $"line '{line.LineKey}' of {readers[line.ReportVersionId]}")
        ];
    }

    /// <summary>The live report versions that default to a profile.</summary>
    /// <param name="profile">A source profile of a model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report version.</returns>
    public async Task<IEnumerable<string>> ProfileDefaultsAsync(SourceProfileRecord profile, CancellationToken cancellationToken)
    {
        var readers = await LiveReadersAsync(profile.ModelVersionId, cancellationToken);
        var defaulting = await versions.ListAsync(new { ReportVersionId = readers.Keys.ToArray(), DefaultProfileKey = profile.ProfileKey }, cancellationToken);
        return defaulting.Select(version => readers[version.ReportVersionId]);
    }

    /// <summary>
    /// The published report versions that read a model version's model, by ID with their names, when the model version is
    /// published; none otherwise, because reports read the published version.
    /// </summary>
    private async Task<IReadOnlyDictionary<long, string>> LiveReadersAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var model = await modelVersions.GetAsync(new { ModelVersionId = modelVersionId }, cancellationToken);
        if (model.Status != PublicationStatuses.Published) return new Dictionary<long, string>();
        return await NamedAsync(await versions.ListAsync(new { model.ModelId, Status = PublicationStatuses.Published }, cancellationToken), cancellationToken);
    }

    /// <summary>Report versions by ID, named like <c>'bank.margin' v2</c>.</summary>
    private async Task<IReadOnlyDictionary<long, string>> NamedAsync(IReadOnlyList<ReportVersionRecord> readers, CancellationToken cancellationToken)
    {
        var keys = (await reports.ListAsync(new { ReportId = readers.Select(version => version.ReportId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        return readers.ToDictionary(version => version.ReportVersionId, version => $"'{keys[version.ReportId]}' v{version.VersionNumber}");
    }
}
