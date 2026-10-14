namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>A table as a CREATE TABLE statement declares it.</summary>
internal sealed record DdlTable
{
    /// <summary>The table name, such as <c>qe_dataset_version</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The columns, in declaration order.</summary>
    public required IReadOnlyList<DdlColumn> Columns { get; init; }

    /// <summary>The PRIMARY KEY columns, in key order.</summary>
    public required IReadOnlyList<string> PrimaryKey { get; init; }
}
