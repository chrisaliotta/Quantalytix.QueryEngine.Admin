namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.MaterializedViews;

/// <summary>A row of <c>information_schema.tables</c> with an object's creation time.</summary>
internal sealed record InformationSchemaViewCreation
{
    /// <summary>The object's name.</summary>
    public required string TableName { get; init; }

    /// <summary>When the object was created.</summary>
    public required DateTime CreateTime { get; init; }
}
