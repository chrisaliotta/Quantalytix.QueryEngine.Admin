using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>dimension_version_in_snapshot</c>: the mapping set's dimension has a version in the mapping version's snapshot, because the
/// engine resolves the entries' member keys in it.
/// </summary>
public sealed class DimensionVersionInSnapshotRule : MappingRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_version_in_snapshot";

    /// <summary>Reports a dimension without a version in the snapshot once; the member rules then report nothing.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>One violation when the dimension has no version in the snapshot; otherwise none.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.DimensionKeys is not null) yield break;
        var dimension = snapshot.Surroundings.Dimension;
        yield return MappingViolations.Of(Code, $"Dimension '{dimension.DimensionKey}' has no version in this snapshot for the entries to resolve in.", MappingAreas.Entries);
    }
}
