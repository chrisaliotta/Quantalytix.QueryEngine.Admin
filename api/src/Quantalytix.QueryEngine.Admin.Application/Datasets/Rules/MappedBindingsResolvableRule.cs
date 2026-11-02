using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>mapped_bindings_resolvable</c>: the mapping set of every mapped binding has a version in the snapshot, because the engine maps
/// source keys through it.
/// </summary>
public sealed class MappedBindingsResolvableRule : BindingIdentityRule
{
    /// <summary><c>mapped_bindings_resolvable</c>.</summary>
    protected override string Code => "mapped_bindings_resolvable";

    /// <summary><c>mapping set</c>.</summary>
    protected override string IdentityKind => "mapping set";

    /// <summary>The mapping set of a mapped binding.</summary>
    /// <param name="binding">A binding.</param>
    /// <returns>The mapping set, or null for any other binding.</returns>
    protected override long? IdentityOf(DimensionBindingRecord binding)
    {
        return binding.BindingKind == BindingKinds.Mapped ? binding.MappingSetId : null;
    }

    /// <summary>The mapping sets with a version in the snapshot.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <returns>The mapping set ids.</returns>
    protected override IReadOnlySet<long> Held(DatasetSurroundings surroundings)
    {
        return surroundings.HeldMappingSetIds;
    }

    /// <summary>A mapping set's key.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <param name="identityId">The mapping set.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(DatasetSurroundings surroundings, long identityId)
    {
        return surroundings.MappingSets.Single(mappingSet => mappingSet.MappingSetId == identityId).MappingSetKey;
    }
}
