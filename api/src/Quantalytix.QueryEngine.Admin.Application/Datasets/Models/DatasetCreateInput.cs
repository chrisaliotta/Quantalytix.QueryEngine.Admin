using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetCreateInput</c> union, discriminated by <c>mode</c>: a dataset over an existing object or a new managed table.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "mode")]
[JsonDerivedType(typeof(DatasetCreateFromExistingInput), "existing")]
[JsonDerivedType(typeof(DatasetCreateWithNewTableInput), "new")]
public abstract record DatasetCreateInput
{
    /// <summary>The dataset key, fixed once created (OI-36).</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }
}

/// <summary><c>existing</c>: registers a table or view the tenant database has.</summary>
public sealed record DatasetCreateFromExistingInput : DatasetCreateInput
{
    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>The table or view.</summary>
    public required string SourceObject { get; init; }
}

/// <summary><c>new</c>: creates and registers a managed table with these columns (OI-33).</summary>
public sealed record DatasetCreateWithNewTableInput : DatasetCreateInput
{
    /// <summary>The new table's name.</summary>
    public required string TableName { get; init; }

    /// <summary>The table's columns, each also a column field of draft version 1; possibly none.</summary>
    public required IReadOnlyList<DatasetNewColumnInput> Columns { get; init; }
}
