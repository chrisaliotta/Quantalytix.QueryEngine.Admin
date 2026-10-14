using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires the table or view a dataset version queries to exist in the tenant database, with the version's kind.</summary>
public sealed class SourceObjectExistsRule(ITableGateway<DatasetVersionRecord> versions, SourceObjectService sourceObjects) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "source_object_exists";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports a source object the tenant database does not have.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the source object is missing; otherwise none.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        if (await sourceObjects.ExistsAsync(version.SourceObjectKind, version.SourceObject, cancellationToken)) return [];
        return [new RuleViolation { Rule = Code, Message = $"The {version.SourceObjectKind} '{version.SourceObject}' does not exist in the tenant database." }];
    }
}
