using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>dimension_version_published</c>: the mapping set's dimension has a live version, because the active assumption set pins it and
/// the engine resolves the entries' member keys in it.
/// </summary>
public sealed class DimensionVersionPublishedRule : MappingRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_version_published";

    /// <summary>Reports a dimension without a live version once; the member rules then report nothing.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>One violation when the dimension has no live version; otherwise none.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.Published is not null) yield break;
        var dimension = snapshot.Surroundings.Dimension;
        yield return MappingViolations.Of(Code, $"Dimension '{dimension.DimensionKey}' has no live version for the entries to resolve in.", MappingAreas.Entries);
    }
}
