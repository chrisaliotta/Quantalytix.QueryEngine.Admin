using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>range_data_type_matches_kind</c>: a version of a <c>range</c> dimension has a range data type, which picks the bound
/// columns its bands compare, and a version of any other kind has none.
/// </summary>
public sealed class RangeDataTypeMatchesKindRule(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionRecord> dimensions) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "range_data_type_matches_kind";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports a range version without a range data type, or another kind's version with one.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation on a mismatch; none otherwise.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var dimension = await dimensions.GetAsync(new { version.DimensionId }, cancellationToken);
        var isRange = dimension.DimensionKind == DimensionKinds.Range;
        if (isRange && version.RangeDataType is null)
            return [Violation($"Range dimension '{dimension.DimensionKey}' needs a range_data_type on v{version.VersionNumber}.")];
        if (!isRange && version.RangeDataType is not null)
            return [Violation($"Dimension '{dimension.DimensionKey}' is {dimension.DimensionKind}, so v{version.VersionNumber} must not have a range_data_type.")];
        return [];
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
