using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Names the report rows that use a model's content by ID: columns read variables and calculated measures, formula lines read
/// calculated measures, and report versions read a model version and its default profile. A delete that would leave such a row
/// dangling is refused.
/// </summary>
public sealed class ReportUses(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines)
{
    /// <summary>The report columns that return any of the variables, named with their report version.</summary>
    /// <param name="variableIds">Model variables.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per column.</returns>
    public async Task<IEnumerable<string>> VariableColumnsAsync(IReadOnlyCollection<long> variableIds, CancellationToken cancellationToken)
    {
        return await NamesAsync(await columns.ListAsync(new { VariableId = variableIds }, cancellationToken), column => column.ReportVersionId,
            column => $"column '{column.ColumnKey}'", cancellationToken);
    }

    /// <summary>The report columns and formula lines that read any of the calculated measures, named with their report version.</summary>
    /// <param name="measureIds">Model calculated measures.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per column or line.</returns>
    public async Task<IEnumerable<string>> MeasureUsesAsync(IReadOnlyCollection<long> measureIds, CancellationToken cancellationToken)
    {
        var columnNames = await NamesAsync(await columns.ListAsync(new { CalculatedMeasureId = measureIds }, cancellationToken),
            column => column.ReportVersionId, column => $"column '{column.ColumnKey}'", cancellationToken);
        var lineNames = await NamesAsync(await lines.ListAsync(new { CalculatedMeasureId = measureIds }, cancellationToken),
            line => line.ReportVersionId, line => $"line '{line.LineKey}'", cancellationToken);
        return columnNames.Concat(lineNames);
    }

    /// <summary>The report versions that default to any of the profiles.</summary>
    /// <param name="profileIds">Model source profiles.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report version.</returns>
    public async Task<IEnumerable<string>> ProfileDefaultsAsync(IReadOnlyCollection<long> profileIds, CancellationToken cancellationToken)
    {
        return await VersionNamesAsync(await versions.ListAsync(new { DefaultSourceProfileId = profileIds }, cancellationToken), cancellationToken);
    }

    /// <summary>The report versions that read any of the model versions.</summary>
    /// <param name="modelVersionIds">Model versions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report version.</returns>
    public async Task<IEnumerable<string>> ModelVersionReadersAsync(IReadOnlyCollection<long> modelVersionIds, CancellationToken cancellationToken)
    {
        return await VersionNamesAsync(await versions.ListAsync(new { ModelVersionId = modelVersionIds }, cancellationToken), cancellationToken);
    }

    /// <summary>Report versions named like <c>'bank.margin' v2</c>.</summary>
    private async Task<IEnumerable<string>> VersionNamesAsync(IReadOnlyList<ReportVersionRecord> readers, CancellationToken cancellationToken)
    {
        var keys = await ReportKeysAsync(readers.Select(version => version.ReportId), cancellationToken);
        return readers.Select(version => $"'{keys[version.ReportId]}' v{version.VersionNumber}");
    }

    /// <summary>Report rows named like <c>column 'yield' of 'bank.margin' v2</c>.</summary>
    private async Task<IEnumerable<string>> NamesAsync<TRow>(
        IReadOnlyList<TRow> rows, Func<TRow, long> versionOf, Func<TRow, string> nameOf, CancellationToken cancellationToken)
    {
        var owners = (await versions.ListAsync(new { ReportVersionId = rows.Select(versionOf).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(version => version.ReportVersionId);
        var keys = await ReportKeysAsync(owners.Values.Select(version => version.ReportId), cancellationToken);
        return rows.Select(row => $"{nameOf(row)} of '{keys[owners[versionOf(row)].ReportId]}' v{owners[versionOf(row)].VersionNumber}");
    }

    /// <summary>The keys of reports by ID.</summary>
    private async Task<Dictionary<long, string>> ReportKeysAsync(IEnumerable<long> reportIds, CancellationToken cancellationToken)
    {
        var rows = await reports.ListAsync(new { ReportId = reportIds.Distinct().ToArray() }, cancellationToken);
        return rows.ToDictionary(report => report.ReportId, report => report.ReportKey);
    }
}
