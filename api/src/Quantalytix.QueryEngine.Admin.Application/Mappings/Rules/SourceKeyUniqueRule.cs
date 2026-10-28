using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>source_key_unique</c>: each source value appears in one entry of the version, compared exactly as text (B2), because a source
/// value maps to exactly one member and StarRocks does not enforce the declared unique key.
/// </summary>
public sealed class SourceKeyUniqueRule : MappingRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "source_key_unique";

    /// <summary>Reports every entry after the first that repeats a source value.</summary>
    /// <param name="snapshot">The mapping version.</param>
    /// <returns>One violation per repeating entry.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(MappingSetSnapshot snapshot)
    {
        return snapshot.Entries
            .GroupBy(entry => entry.SourceKeyValue, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1).Select(entry => MappingViolations.Of(
                Code,
                $"Source key '{group.Key}' appears in {group.Count()} entries.",
                MappingAreas.Entries,
                entry.DimensionMappingId,
                filter: group.Key)));
    }
}
