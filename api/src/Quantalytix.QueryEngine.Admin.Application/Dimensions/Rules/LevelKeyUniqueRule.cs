using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>level_key_unique</c>: level keys are unique within the version, so each names one level.</summary>
public sealed class LevelKeyUniqueRule(ITableGateway<DimensionLevelRecord> levels) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "level_key_unique";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every level key that more than one level of the version uses.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per repeated key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await levels.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        return
        [
            .. rows
                .GroupBy(level => level.LevelKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleViolation { Code = Code, Message = $"Level key '{group.Key}' is used by {group.Count()} levels." })
        ];
    }
}
