using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires a dataset version to support aggregate queries, detail queries or both; otherwise no query can use it.</summary>
public sealed class VersionSupportsAModeRule(ITableGateway<DatasetVersionRecord> versions) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "version_supports_a_mode";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports a version that supports neither query mode.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation when both modes are off; otherwise none.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        if (version.SupportsAggregate || version.SupportsDetail) return [];
        return [new RuleViolation { Rule = Code, Message = $"Version v{version.VersionNumber} supports neither aggregate nor detail queries; enable at least one." }];
    }
}
