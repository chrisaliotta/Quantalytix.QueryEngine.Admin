using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// The tables and views datasets can read, with their columns typed as fields, the managed-table registry (OI-33) and live
/// statistics (OI-34). The query engine's own metadata objects, its materialized views included, are never dataset sources.
/// </summary>
public sealed class SourceObjectDirectory(ISourceObjectCatalog catalog, ITableGateway<ManagedSourceObjectRecord> managed)
{
    /// <summary>The name prefixes of the metadata tables and materialized views (<c>qe_</c>) and views (<c>vw_qe_</c>).</summary>
    private static readonly string[] MetadataPrefixes = ["qe_", "vw_qe_"];

    /// <summary>Lists the tables and views datasets can read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every table and view except the metadata objects, by name.</returns>
    public async Task<IReadOnlyList<SourceObject>> ObjectsAsync(CancellationToken cancellationToken)
    {
        var objects = await catalog.ListObjectsAsync(cancellationToken);
        return [.. objects.Where(item => !IsMetadata(item.Name)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Lists the tables and views datasets can read, with whether the API manages each and its statistics.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The objects, by name.</returns>
    public async Task<IReadOnlyList<DatasetSourceObject>> ListAsync(CancellationToken cancellationToken)
    {
        var registry = await ManagedAsync(cancellationToken);
        var statistics = await catalog.ListStatisticsAsync(cancellationToken);
        return
        [
            .. (await ObjectsAsync(cancellationToken)).Select(item => new DatasetSourceObject
            {
                Name = item.Name,
                Kind = item.Kind,
                IsManaged = IsManaged(registry, item.Kind, item.Name),
                Stats = statistics.GetValueOrDefault(item.Name)
            })
        ];
    }

    /// <summary>Lists the columns of a table or view with the field type each reads as.</summary>
    /// <param name="kind">A <c>source_object_kind</c> value.</param>
    /// <param name="name">The object's name.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The columns in ordinal order, or null when datasets can read no object of that kind and name.</returns>
    public async Task<IReadOnlyList<DatasetSourceColumn>?> ColumnsAsync(string kind, string name, CancellationToken cancellationToken)
    {
        if (!(await ObjectsAsync(cancellationToken)).Any(item => item.Kind == kind && item.Name == name)) return null;
        var isManaged = IsManaged(await ManagedAsync(cancellationToken), kind, name);
        return [.. (await catalog.ListColumnsAsync(name, cancellationToken)).Select(column => Column(column, isManaged))];
    }

    /// <summary>Lists the columns of every table and view.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each object's columns, by object name.</returns>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>>> AllColumnsAsync(CancellationToken cancellationToken)
    {
        return catalog.ListAllColumnsAsync(cancellationToken);
    }

    /// <summary>Reads the storage statistics of the tables and views.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each measured object's statistics, by name.</returns>
    public Task<IReadOnlyDictionary<string, DatasetSourceObjectStats>> StatisticsAsync(CancellationToken cancellationToken)
    {
        return catalog.ListStatisticsAsync(cancellationToken);
    }

    /// <summary>Reads the managed-table registry.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every registered object.</returns>
    public Task<IReadOnlyList<ManagedSourceObjectRecord>> ManagedAsync(CancellationToken cancellationToken)
    {
        return managed.ListAsync(null, cancellationToken);
    }

    /// <summary>A physical column with the field type it reads as.</summary>
    /// <param name="column">The column.</param>
    /// <param name="isManaged">Whether its object is a managed table, all of whose columns the API added.</param>
    /// <returns>The contract's column.</returns>
    public static DatasetSourceColumn Column(PhysicalColumn column, bool isManaged)
    {
        var type = ColumnTypeMapping.Map(column.ColumnType);
        return new DatasetSourceColumn
        {
            Name = column.Name,
            NativeType = column.ColumnType,
            MappedType = type?.DataType,
            NumericPrecision = type?.NumericPrecision,
            NumericScale = type?.NumericScale,
            IsNullable = column.IsNullable,
            IsGenerated = isManaged
        };
    }

    /// <summary>Whether the registry holds an object of a kind and name.</summary>
    private static bool IsManaged(IReadOnlyList<ManagedSourceObjectRecord> registry, string kind, string name)
    {
        return registry.Any(row => row.SourceObjectKind == kind && row.SourceObject == name);
    }

    /// <summary>Whether a name belongs to the query engine's metadata objects, which datasets never read.</summary>
    /// <param name="name">A table or view name.</param>
    /// <returns>True for a <c>qe_</c> or <c>vw_qe_</c> name.</returns>
    public static bool IsMetadata(string name)
    {
        return MetadataPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
    }
}
