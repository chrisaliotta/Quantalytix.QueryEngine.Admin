using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The shape of the three binding identity rules: every binding that resolves through an identity of one kind needs that identity to
/// have a version in the dataset version's snapshot, because the engine resolves the binding there.
/// </summary>
public abstract class BindingIdentityRule : BindingRule
{
    /// <summary>The kind of identity, worded for the message, such as <c>mapping set</c>.</summary>
    protected abstract string IdentityKind { get; }

    /// <summary>Reports every binding whose identity has no version in the snapshot.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>One violation per unresolvable binding.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var held = Held(snapshot.Surroundings);
        return ByName(snapshot.Bindings, names)
            .Where(binding => IdentityOf(binding) is { } identityId && !held.Contains(identityId))
            .Select(binding => Violation(
                binding,
                names,
                $"Binding '{names[binding.DimensionBindingId]}' resolves through {IdentityKind} '{KeyOf(snapshot.Surroundings, IdentityOf(binding)!.Value)}', which has no version in this snapshot."));
    }

    /// <summary>The identity a binding resolves through; null when the binding needs none of this kind.</summary>
    /// <param name="binding">A binding.</param>
    /// <returns>The identity's id, or null.</returns>
    protected abstract long? IdentityOf(DimensionBindingRecord binding);

    /// <summary>The identities of this kind with a version in the snapshot.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <returns>The identity ids.</returns>
    protected abstract IReadOnlySet<long> Held(DatasetSurroundings surroundings);

    /// <summary>An identity's key.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <param name="identityId">The identity.</param>
    /// <returns>Its key.</returns>
    protected abstract string KeyOf(DatasetSurroundings surroundings, long identityId);
}
