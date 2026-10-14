namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The generated column to add to a table for a <c>json_path</c> field.</summary>
public sealed record MaterializeInput
{
    /// <summary>The new column's name; the source table must not have a column of that name yet.</summary>
    public required string ColumnName { get; init; }
}
