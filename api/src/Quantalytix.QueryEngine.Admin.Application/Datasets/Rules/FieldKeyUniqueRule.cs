using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Requires field keys to be unique within a dataset version: a request names fields by key, and the query engine loads a
/// version's fields into a dictionary keyed by it.
/// </summary>
public sealed class FieldKeyUniqueRule(ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "field_key_unique";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each field key that more than one field of the version uses.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per repeated key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return
        [
            .. rows
                .GroupBy(field => field.FieldKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleViolation { Rule = Code, Message = $"Field key '{group.Key}' is used by {group.Count()} fields of this version." })
        ];
    }
}
