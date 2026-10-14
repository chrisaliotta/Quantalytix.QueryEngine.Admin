namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>One column of a CREATE TABLE statement.</summary>
internal sealed record DdlColumn
{
    /// <summary>The column name, such as <c>dataset_version_id</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The SQL type without its length or precision, such as <c>VARCHAR</c> or <c>DECIMAL</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Whether the column is declared <c>NULL</c> rather than <c>NOT NULL</c>.</summary>
    public required bool IsNullable { get; init; }
}
