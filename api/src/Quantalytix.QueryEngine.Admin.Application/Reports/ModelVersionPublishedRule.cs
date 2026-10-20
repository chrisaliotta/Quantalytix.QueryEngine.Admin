using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>model_version_published</c>: the report's calculation model has a published version, because the active assumption set
/// pins it and every run that names no set reads it.
/// </summary>
public sealed class ModelVersionPublishedRule(ITableGateway<ReportVersionRecord> versions, ITableGateway<CalculationModelRecord> models, ModelMetricsReader metrics)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "model_version_published";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports a model with no published version.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the model has no published version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        if (await metrics.PublishedVersionAsync(version.ModelId, cancellationToken) is not null) return [];
        var model = await models.GetAsync(new { version.ModelId }, cancellationToken);
        return [new RuleViolation { Rule = Code, Message = $"Calculation model '{model.ModelKey}' has no published version; publish one first." }];
    }
}
