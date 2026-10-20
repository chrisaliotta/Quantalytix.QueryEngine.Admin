namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>How a variable source's dataset rows reach the members of the report's dimension.</summary>
public sealed record ClassificationInput
{
    /// <summary>A <c>classification_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The dataset field a <c>direct</c> or <c>mapped</c> classification reads; null for <c>bridge</c>, which reads its role's binding.</summary>
    public string? FieldKey { get; init; }

    /// <summary>
    /// The role a <c>bridge</c> classification starts from, whose template must be the bridge's source dimension; the source's
    /// dataset version binds it by member. Null for every other kind.
    /// </summary>
    public long? RoleId { get; init; }

    /// <summary>The mapping set of a <c>mapped</c> classification, which must map into the report's dimension.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The bridge of a <c>bridge</c> classification, whose target must be the report's dimension.</summary>
    public long? BridgeId { get; init; }
}
