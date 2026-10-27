using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Requires the time binding a measure's time policy requires to be a time binding of the same dataset version: a binding
/// with a time extension, because the query engine resolves the required time axis among the version's time bindings.
/// </summary>
public sealed class TimePolicyTimeBindingInVersionRule(
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "time_policy_time_binding_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each required time binding that is not a time binding of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per time policy requiring a foreign binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var measureKeys = (await measures.ListAsync(versionFilter, cancellationToken)).ToDictionary(measure => measure.MeasureId, measure => measure.MeasureKey);
        var requiring = (await timePolicies.ListAsync(new { MeasureId = measureKeys.Keys.ToArray() }, cancellationToken))
            .Where(policy => policy.RequiredTimeBindingId is not null)
            .OrderBy(policy => measureKeys[policy.MeasureId], StringComparer.Ordinal)
            .ToList();
        var versionBindingIds = (await bindings.ListAsync(versionFilter, cancellationToken)).Select(binding => binding.DimensionBindingId).ToArray();
        var timeBindingIds = (await timeBindings.ListAsync(new { DimensionBindingId = versionBindingIds }, cancellationToken))
            .Select(extension => extension.DimensionBindingId)
            .ToHashSet();
        return
        [
            .. requiring
                .Where(policy => !timeBindingIds.Contains(policy.RequiredTimeBindingId!.Value))
                .Select(policy => new RuleViolation
                {
                    Code = Code,
                    Message = $"The time policy of measure '{measureKeys[policy.MeasureId]}' requires binding {policy.RequiredTimeBindingId}, " +
                        "which is not a time binding of this version."
                })
        ];
    }
}
