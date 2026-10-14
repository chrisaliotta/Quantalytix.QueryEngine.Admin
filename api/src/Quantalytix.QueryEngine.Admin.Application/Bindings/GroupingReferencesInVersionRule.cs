using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the grouping level, grouping ancestor member and unknown member of a binding to belong to the dimension version
/// the binding resolves: its pinned version or, when it pins none, the published version of its dimension whose
/// <c>[valid_from, valid_to)</c> window contains now. The query engine binds these references by raw ID, so a level or
/// member of another version would group or route nothing.
/// </summary>
public sealed class GroupingReferencesInVersionRule(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    TimeProvider time) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "grouping_references_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>
    /// Reports each level or member reference outside the binding's resolved dimension version, and each unpinned binding
    /// with references whose dimension has no published version in effect.
    /// </summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per foreign reference, or one per binding that resolves no version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var referencing = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.GroupingLevelId is not null || binding.GroupingAncestorMemberId is not null || binding.UnknownMemberId is not null)
            .OrderBy(binding => binding.DisplayName, StringComparer.Ordinal)
            .ToList();
        var versionsOfDimensions = await dimensionVersions.ListAsync(
            new { DimensionId = referencing.Select(binding => binding.DimensionId).Distinct().ToArray() },
            cancellationToken);
        long[] levelIds = [.. referencing.Select(binding => binding.GroupingLevelId).OfType<long>().Distinct()];
        var levelVersions = (await levels.ListAsync(new { LevelId = levelIds }, cancellationToken))
            .ToDictionary(level => level.LevelId, level => level.DimensionVersionId);
        long[] memberIds = [.. referencing.SelectMany(binding => new[] { binding.GroupingAncestorMemberId, binding.UnknownMemberId }).OfType<long>().Distinct()];
        var memberVersions = (await members.ListAsync(new { MemberId = memberIds }, cancellationToken))
            .ToDictionary(member => member.MemberId, member => member.DimensionVersionId);
        var now = time.GetUtcNow().UtcDateTime;

        var violations = new List<RuleViolation>();
        foreach (var binding in referencing)
        {
            var name = binding.DisplayName;
            var resolvedId = binding.PinnedDimensionVersionId ?? EffectiveVersion(versionsOfDimensions, binding.DimensionId, now)?.DimensionVersionId;
            if (resolvedId is not { } dimensionVersionId)
            {
                violations.Add(Violation(
                    $"Binding '{name}' names levels or members of its dimension, which has no published version in effect now; pin the version that holds them."));
                continue;
            }

            var version = Describe(binding, dimensionVersionId, versionsOfDimensions);
            if (binding.GroupingLevelId is { } levelId && !BelongsTo(levelVersions, levelId, dimensionVersionId))
            {
                violations.Add(Violation($"Binding '{name}' groups at level {levelId}, which is not a level of {version}."));
            }

            if (binding.GroupingAncestorMemberId is { } ancestorId && !BelongsTo(memberVersions, ancestorId, dimensionVersionId))
            {
                violations.Add(Violation($"Binding '{name}' groups under ancestor member {ancestorId}, which is not a member of {version}."));
            }

            if (binding.UnknownMemberId is { } unknownId && !BelongsTo(memberVersions, unknownId, dimensionVersionId))
            {
                violations.Add(Violation($"Binding '{name}' routes unmapped rows to member {unknownId}, which is not a member of {version}."));
            }
        }

        return violations;
    }

    /// <summary>The published version of a dimension whose validity window contains a time, if there is one.</summary>
    private static DimensionVersionRecord? EffectiveVersion(IEnumerable<DimensionVersionRecord> versions, long dimensionId, DateTime now)
    {
        return versions.FirstOrDefault(version =>
            version.DimensionId == dimensionId
            && version.Status == PublicationStatuses.Published
            && version.ValidFrom <= now
            && (version.ValidTo is null || now < version.ValidTo));
    }

    /// <summary>Whether a level or member, by its owning version, belongs to a dimension version.</summary>
    private static bool BelongsTo(Dictionary<long, long> owners, long id, long dimensionVersionId)
    {
        return owners.TryGetValue(id, out var owner) && owner == dimensionVersionId;
    }

    /// <summary>The resolved version as messages name it, such as <c>its pinned version v2</c>.</summary>
    private static string Describe(DimensionBindingRecord binding, long dimensionVersionId, IReadOnlyList<DimensionVersionRecord> versions)
    {
        var name = $"dimension version {dimensionVersionId}";
        if (versions.FirstOrDefault(version => version.DimensionVersionId == dimensionVersionId) is { } found) name = $"v{found.VersionNumber}";
        if (binding.PinnedDimensionVersionId is not null) return $"its pinned version {name}";
        return $"{name}, the version of its dimension in effect now";
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
