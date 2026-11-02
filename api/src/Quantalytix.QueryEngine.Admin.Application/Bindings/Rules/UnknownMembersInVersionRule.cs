using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// <c>unknown_members_in_version</c>: a binding that routes unmapped rows to a member names a member of its dimension's version in
/// the dataset version's snapshot. The members live outside the aggregate, so the rule reads them through the dimensions module's
/// reader.
/// </summary>
/// <param name="dimensions">Reads a dimension's version in a snapshot and its member keys.</param>
public sealed class UnknownMembersInVersionRule(SnapshotDimensionReader dimensions) : ISnapshotRule<DatasetSnapshot>
{
    /// <summary>The rule code.</summary>
    private const string Code = "unknown_members_in_version";

    /// <summary>Commit: findings refuse the save.</summary>
    public RuleStage Stage => RuleStage.Commit;

    /// <summary>Reports every routed member the bound dimension's version lacks.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the member reads.</param>
    /// <returns>One violation per such binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DatasetSnapshot snapshot, CancellationToken cancellationToken)
    {
        var names = DatasetBindingNames.Of(snapshot);
        var routing = snapshot.Bindings.Where(binding => binding.UnknownMemberKey is not null).OrderBy(binding => names[binding.DimensionBindingId], StringComparer.Ordinal).ToList();
        var held = new Dictionary<long, DimensionKeys?>();
        foreach (var dimensionId in routing.Select(binding => binding.DimensionId).Distinct())
            held[dimensionId] = await dimensions.ReadAsync(dimensionId, snapshot.Version.SnapshotId, cancellationToken);

        var violations = new List<RuleViolation>();
        foreach (var binding in routing)
        {
            var name = names[binding.DimensionBindingId];
            string? message = held[binding.DimensionId] is not { } keys
                ? $"Binding '{name}' routes unmapped rows to a member of its dimension, which has no version in this snapshot."
                : keys.MemberKeys.Contains(binding.UnknownMemberKey!)
                    ? null
                    : $"Binding '{name}' routes unmapped rows to member '{binding.UnknownMemberKey}', which is not a member of its dimension's version.";
            if (message is null) continue;
            violations.Add(new RuleViolation
            {
                Code = Code,
                Message = message,
                Target = new ViolationTarget { Area = DatasetTabs.Bindings, ItemId = TemporaryIds.PresentStored(binding.DimensionBindingId), ItemKey = name }
            });
        }

        return violations;
    }
}
