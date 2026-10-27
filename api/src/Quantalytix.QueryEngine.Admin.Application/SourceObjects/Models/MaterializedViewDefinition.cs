namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>A materialized view over one source object: every column of the object plus the extracted JSON path columns.</summary>
public sealed record MaterializedViewDefinition
{
    /// <summary>The view's name.</summary>
    public required string ViewName { get; init; }

    /// <summary>The table or view the view reads.</summary>
    public required string SourceObject { get; init; }

    /// <summary>The extracted columns, each named once.</summary>
    public required IReadOnlyList<MaterializedViewColumn> Columns { get; init; }
}
