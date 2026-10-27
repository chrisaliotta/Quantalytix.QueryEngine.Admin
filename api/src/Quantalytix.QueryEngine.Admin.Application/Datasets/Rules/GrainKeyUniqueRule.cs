using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires grain keys to be unique within a dataset version, which is the grain's logical identity (schema section 7).</summary>
public sealed class GrainKeyUniqueRule(ITableGateway<DatasetGrainRecord> grains) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "grain_key_unique";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each grain key that more than one grain of the version uses.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per repeated key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await grains.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return
        [
            .. rows
                .GroupBy(grain => grain.GrainKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleViolation { Code = Code, Message = $"Grain key '{group.Key}' is used by {group.Count()} grains of this version." })
        ];
    }
}
