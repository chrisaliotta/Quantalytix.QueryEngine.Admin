using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a write to the members of a dimension version against the live objects that name keys in the dimension. They resolve
/// their keys in the published version, which stays editable, so a write to it is applied to its members in memory first and
/// refused when a key some object needs would stop resolving as the object needs it, as <c>dimension_consumers_resolve</c> checks a
/// version being published. A draft resolves nothing, so writes to it break nothing.
/// </summary>
/// <param name="versions">Reads the version.</param>
/// <param name="levels">Reads the version's levels.</param>
/// <param name="members">Reads the version's members.</param>
/// <param name="consumers">Lists the keys live objects name in the dimension.</param>
public sealed class DimensionChanges(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    DimensionConsumers consumers)
{
    /// <summary>The references a write would break: those the version resolves now and would not once the write changed its members.</summary>
    /// <param name="dimensionVersionId">The version written to.</param>
    /// <param name="change">The write's effect on the version's members.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The broken references; none for a version that is not published.</returns>
    public async Task<IReadOnlyList<KeyReference>> BrokenAsync(
        long dimensionVersionId,
        Func<IReadOnlyList<DimensionMemberRecord>, IReadOnlyList<DimensionMemberRecord>> change,
        CancellationToken cancellationToken)
    {
        var owner = new { DimensionVersionId = dimensionVersionId };
        var version = await versions.GetAsync(owner, cancellationToken);
        if (version.Status != PublicationStatuses.Published) return [];
        string[] levelKeys = [.. (await levels.ListAsync(owner, cancellationToken)).Select(level => level.LevelKey)];
        var stored = await members.ListAsync(owner, cancellationToken);
        var before = DimensionKeys.Of(version, levelKeys, stored);
        var after = DimensionKeys.Of(version, levelKeys, change(stored));
        return [.. (await consumers.ListAsync(version.DimensionId, cancellationToken)).Where(reference => before.Resolves(reference) && !after.Resolves(reference))];
    }

    /// <summary>Checks a write to a version's members, which is made only when it breaks no reference.</summary>
    /// <param name="dimensionVersionId">The version written to.</param>
    /// <param name="change">The write's effect on the version's members.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One <c>dimension_consumers_resolve</c> violation per reference the write would break; none when it may be made.</returns>
    public async Task<IReadOnlyList<RuleViolation>> CheckAsync(
        long dimensionVersionId,
        Func<IReadOnlyList<DimensionMemberRecord>, IReadOnlyList<DimensionMemberRecord>> change,
        CancellationToken cancellationToken)
    {
        var broken = await BrokenAsync(dimensionVersionId, change, cancellationToken);
        if (broken.Count == 0) return [];
        var version = await versions.GetAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        return [.. broken.Select(reference => DimensionConsumersResolveRule.Violation(reference, version))];
    }
}
