namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>A table or view of the tenant database that a dataset version can query.</summary>
public sealed record SourceObject
{
    /// <summary>The object's name in the tenant database.</summary>
    public required string Name { get; init; }

    /// <summary>A <c>source_object_kind</c> value: <c>table</c> or <c>view</c>.</summary>
    public required string Kind { get; init; }
}
