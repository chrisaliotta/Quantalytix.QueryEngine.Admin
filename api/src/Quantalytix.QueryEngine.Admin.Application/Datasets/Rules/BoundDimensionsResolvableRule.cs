using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>bound_dimensions_resolvable</c>: the dimension of every binding has a version in the snapshot. Time bindings are exempt: the
/// engine derives their calendar periods from the bound field.
/// </summary>
public sealed class BoundDimensionsResolvableRule : BindingIdentityRule
{
    /// <summary><c>bound_dimensions_resolvable</c>.</summary>
    protected override string Code => "bound_dimensions_resolvable";

    /// <summary><c>dimension</c>.</summary>
    protected override string IdentityKind => "dimension";

    /// <summary>The bound dimension of every binding but a time binding.</summary>
    /// <param name="binding">A binding.</param>
    /// <returns>The dimension, or null for a time binding.</returns>
    protected override long? IdentityOf(DimensionBindingRecord binding)
    {
        return binding.BindingKind == BindingKinds.Time ? null : binding.DimensionId;
    }

    /// <summary>The dimensions with a version in the snapshot.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <returns>The dimension ids.</returns>
    protected override IReadOnlySet<long> Held(DatasetSurroundings surroundings)
    {
        return surroundings.HeldDimensionIds;
    }

    /// <summary>A dimension's key.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <param name="identityId">The dimension.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(DatasetSurroundings surroundings, long identityId)
    {
        return surroundings.Dimensions.Single(dimension => dimension.DimensionId == identityId).DimensionKey;
    }
}
