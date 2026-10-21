using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Names the reports that read a model. Every report of the model names the model itself, so the model stays while any report
/// reads it; what its reports name of its metrics, <see cref="ModelChanges"/> keeps resolving on every edit.
/// </summary>
public sealed class ReportUses(ITableGateway<ReportRecord> reports)
{
    /// <summary>The reports, enabled or not, that read a model.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One name per report, like <c>report 'bank.margin_statement'</c>.</returns>
    public async Task<IEnumerable<string>> ModelReadersAsync(long modelId, CancellationToken cancellationToken)
    {
        return ReportNames.Of(await reports.ListAsync(new { ModelId = modelId }, cancellationToken)).Values;
    }
}
