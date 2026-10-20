namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>How a source's or route's dataset rows reach the members of the canonical dimension it classifies into.</summary>
public sealed record ClassificationInput
{
    /// <summary>A <c>classification_kind</c> value for a source; a <c>route_kind</c> value for a route.</summary>
    public required string Kind { get; init; }

    /// <summary>The dataset field the classification reads; for <c>bridge</c>, a field whose binding groups by member into the bridge's source dimension.</summary>
    public required string FieldKey { get; init; }

    /// <summary>The mapping set of a <c>mapped</c> classification, which must map into the target dimension.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The bridge of a <c>bridge</c> classification, whose target must be the target dimension.</summary>
    public long? BridgeId { get; init; }
}
