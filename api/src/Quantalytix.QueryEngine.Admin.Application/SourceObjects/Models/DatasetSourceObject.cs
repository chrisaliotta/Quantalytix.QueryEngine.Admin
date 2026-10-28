namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>The contract's <c>DatasetSourceObject</c>: a table or view of the tenant database a dataset version can read.</summary>
public sealed record DatasetSourceObject
{
    /// <summary>The object's name.</summary>
    public required string Name { get; init; }

    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>Whether the API created the object and registered it, so fields may add columns to it (OI-33).</summary>
    public required bool IsManaged { get; init; }

    /// <summary>Live storage statistics (OI-34); null when StarRocks has not measured the object.</summary>
    public required DatasetSourceObjectStats? Stats { get; init; }
}
