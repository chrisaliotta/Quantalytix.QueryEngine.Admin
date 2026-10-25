using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>source_key_unique</c>: each source value appears in one entry of the version, compared exactly, because a source value
/// must map to exactly one member and StarRocks does not enforce the declared unique key.
/// </summary>
public sealed class SourceKeyUniqueRule(ITableGateway<DimensionMappingRecord> entries) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "source_key_unique";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports every source key that more than one entry of the version uses.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per repeated source key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await entries.ListAsync(new { MappingVersionId = versionId }, cancellationToken);
        return
        [
            .. rows
                .GroupBy(entry => entry.SourceKeyValue, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleViolation { Rule = Code, Message = $"Source key '{group.Key}' appears in {group.Count()} entries." })
        ];
    }
}
