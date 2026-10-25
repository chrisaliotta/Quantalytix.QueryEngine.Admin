namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// Offers the tables and views of the tenant database that datasets can query, and their columns typed as fields. The
/// query engine's own metadata tables and views are never dataset sources, so they are left out.
/// </summary>
public sealed class SourceObjectService(ISourceObjectCatalog catalog)
{
    /// <summary>The name prefixes of the metadata tables (<c>qe_</c>) and views (<c>vw_qe_</c>).</summary>
    private static readonly string[] MetadataPrefixes = ["qe_", "vw_qe_"];

    /// <summary>Lists the tables and views datasets can query.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every table and view except the metadata objects, sorted by name.</returns>
    public async Task<IReadOnlyList<SourceObject>> ListAsync(CancellationToken cancellationToken)
    {
        var objects = await catalog.ListObjectsAsync(cancellationToken);
        return [.. objects.Where(item => !IsMetadata(item.Name)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether datasets can query a table or view of the given kind and name.</summary>
    /// <param name="kind">A <c>source_object_kind</c> value.</param>
    /// <param name="name">The table or view name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when <see cref="ListAsync"/> lists an object with that kind and name.</returns>
    public async Task<bool> ExistsAsync(string kind, string name, CancellationToken cancellationToken)
    {
        var objects = await ListAsync(cancellationToken);
        return objects.Any(item => item.Kind == kind && item.Name == name);
    }

    /// <summary>Lists the columns of a table or view with the field type each maps to.</summary>
    /// <param name="objectName">The table or view name; an unknown name has no columns.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The columns, in ordinal order.</returns>
    public async Task<IReadOnlyList<SourceColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken)
    {
        var columns = await catalog.ListColumnsAsync(objectName, cancellationToken);
        return [.. columns.Select(ToSourceColumn)];
    }

    /// <summary>Whether a name belongs to the query engine's metadata tables or views.</summary>
    private static bool IsMetadata(string name)
    {
        return MetadataPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>A physical column with the field type its column type maps to.</summary>
    private static SourceColumn ToSourceColumn(PhysicalColumn column)
    {
        var type = ColumnTypeMapping.Map(column.ColumnType);
        return new SourceColumn
        {
            Name = column.Name,
            ColumnType = column.ColumnType,
            DataType = type?.DataType,
            NumericPrecision = type?.NumericPrecision,
            NumericScale = type?.NumericScale,
            IsNullable = column.IsNullable,
            IsGenerated = column.IsGenerated
        };
    }
}
