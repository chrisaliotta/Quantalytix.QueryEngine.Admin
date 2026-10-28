using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>descendants_report_only</c>: only report bridges map a member with its descendants (B8, OI-28); an analytical bridge maps each
/// source member alone, because the cardinality and allocation rules count each source member's targets.
/// </summary>
public sealed class DescendantsReportOnlyRule : BridgeRule
{
    /// <summary><c>descendants_report_only</c>.</summary>
    protected override string Code => "descendants_report_only";

    /// <summary>Reports every mapping of an analytical bridge that selects descendants; a report bridge has nothing to check.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>One violation per such mapping.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        if (snapshot.IsReportBridge) return [];
        return snapshot.Mappings
            .Where(mapping => mapping.SourceSelection == SourceSelections.Descendants)
            .Select(mapping => OnMapping(mapping, $"Mapping {Describe(mapping)} includes the source member's descendants; only report bridges may."));
    }
}
