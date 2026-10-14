namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// A new draft version of a mapping set, as <c>POST mapping-sets/{mappingSetId}/versions</c> takes it: the dimension version
/// it maps onto, and optionally a version whose entries it copies.
/// </summary>
public sealed record NewMappingVersionInput
{
    /// <summary>The dimension version of the set's dimension that the entries' members come from.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>
    /// The mapping version whose entries the draft copies, each moved to the member with the same key in
    /// <c>dimensionVersionId</c>; null for an empty draft.
    /// </summary>
    public long? CloneFromVersionId { get; init; }
}
