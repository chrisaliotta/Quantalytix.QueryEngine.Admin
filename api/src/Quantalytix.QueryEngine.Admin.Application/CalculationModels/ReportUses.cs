using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Names the reports that read a model. Every report of the model names the model itself, so the model stays while any report
/// reads it; what its reports name of its metrics and parameters, <see cref="ModelChanges"/> keeps resolving on every edit.
/// </summary>
public sealed class ReportUses(ITableGateway<ReportRecord> reports)
{
    /// <summary>The reports, enabled or not, that read a model.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reports by key.</returns>
    public async Task<IReadOnlyList<ReportRecord>> OfModelAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return [.. (await reports.ListAsync(new { CalculationModelId = calculationModelId }, cancellationToken)).OrderBy(report => report.ReportKey, StringComparer.Ordinal)];
    }

    /// <summary>The names of the reports, enabled or not, that read a model.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One name per report, like <c>report 'bank.margin_statement'</c>.</returns>
    public async Task<IEnumerable<string>> ModelReadersAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        return ReportNames.Of(await OfModelAsync(calculationModelId, cancellationToken)).Values;
    }
}
