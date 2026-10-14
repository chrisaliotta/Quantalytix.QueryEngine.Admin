using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Requires the selection grain of a measure's time policy to be a <c>series</c> grain of the same dataset version (schema
/// sections 14 and 18): observations are selected independently per series, and the query engine finds the series fields
/// in the version's grains.
/// </summary>
public sealed class TimePolicySeriesGrainRule(
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    ITableGateway<DatasetGrainRecord> grains) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "time_policy_series_grain";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each selection grain that is not a grain of the version, or not a series grain.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per time policy with an unusable selection grain.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var measureKeys = (await measures.ListAsync(versionFilter, cancellationToken)).ToDictionary(measure => measure.MeasureId, measure => measure.MeasureKey);
        var selecting = (await timePolicies.ListAsync(new { MeasureId = measureKeys.Keys.ToArray() }, cancellationToken))
            .Where(policy => policy.SelectionGrainId is not null)
            .OrderBy(policy => measureKeys[policy.MeasureId], StringComparer.Ordinal)
            .ToList();
        var versionGrains = (await grains.ListAsync(versionFilter, cancellationToken)).ToDictionary(grain => grain.GrainId);

        var violations = new List<RuleViolation>();
        foreach (var policy in selecting)
        {
            var subject = $"The time policy of measure '{measureKeys[policy.MeasureId]}'";
            if (!versionGrains.TryGetValue(policy.SelectionGrainId!.Value, out var grain))
            {
                violations.Add(Violation($"{subject} selects by grain {policy.SelectionGrainId}, which is not a grain of this version."));
            }
            else if (grain.GrainKind != GrainKinds.Series)
            {
                violations.Add(Violation($"{subject} selects by grain '{grain.GrainKey}', a {grain.GrainKind} grain; observations are selected per series grain."));
            }
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
