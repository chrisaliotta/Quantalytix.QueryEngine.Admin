namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// A new draft version of a mapping set, as <c>POST mapping-sets/{mappingSetId}/versions</c> takes it: optionally a version
/// whose entries it copies.
/// </summary>
public sealed record NewMappingVersionInput
{
    /// <summary>A version of the same mapping set whose entries the draft copies; null for an empty draft.</summary>
    public long? CloneFromVersionId { get; init; }
}
