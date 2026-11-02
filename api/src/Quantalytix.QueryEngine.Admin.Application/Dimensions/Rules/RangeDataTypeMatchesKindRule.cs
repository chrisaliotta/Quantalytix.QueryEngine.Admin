using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>range_data_type_matches_kind</c>: a version of a range dimension has a range data type, which picks the bound columns its
/// bands compare, and a version of any other kind has none.
/// </summary>
public sealed class RangeDataTypeMatchesKindRule : DimensionRule
{
    /// <summary>Commit: a range dimension saves only with a range data type.</summary>
    public override RuleStage Stage => RuleStage.Commit;

    /// <summary><c>range_data_type_matches_kind</c>.</summary>
    protected override string Code => "range_data_type_matches_kind";

    /// <summary>Reports a range version without a range data type, or another kind's version with one.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation on a mismatch.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        var isRange = snapshot.Dimension.DimensionKind == DimensionKinds.Range;
        if (isRange && snapshot.Version.RangeDataType is null)
            yield return Violation(DimensionTabs.Properties, $"Range dimension '{snapshot.Dimension.DimensionKey}' needs a range data type.");
        if (!isRange && snapshot.Version.RangeDataType is not null)
            yield return Violation(DimensionTabs.Properties, $"Dimension '{snapshot.Dimension.DimensionKey}' is {snapshot.Dimension.DimensionKind}, so its version must not have a range data type.");
    }
}
