using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>model_version_published</c>: the model version a report version reads is published, because the engine loads only
/// published model content, and a published report must not change under a draft model.
/// </summary>
public sealed class ModelVersionPublishedRule(
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<CalculationModelVersionRecord> modelVersions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "model_version_published";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports a model version that is not published.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the model version is not published.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        var model = await modelVersions.GetAsync(new { version.ModelVersionId }, cancellationToken);
        if (model.Status == PublicationStatuses.Published) return [];
        return [new RuleViolation { Rule = Code, Message = $"The report reads model version v{model.VersionNumber}, which is {model.Status}; publish it first." }];
    }
}
