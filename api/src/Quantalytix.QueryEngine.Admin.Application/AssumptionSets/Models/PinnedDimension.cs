using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>A pin of an assumption set with the key of its dimension, which the assumption set rules name in their messages.</summary>
public sealed record PinnedDimension
{
    /// <summary>The <c>dimension_key</c> of the pin's dimension.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>The pin.</summary>
    public required AssumptionSetDimensionRecord Pin { get; init; }
}
