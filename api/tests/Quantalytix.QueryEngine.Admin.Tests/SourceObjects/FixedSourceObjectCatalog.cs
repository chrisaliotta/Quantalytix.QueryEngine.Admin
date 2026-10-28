using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

/// <summary>
/// An <see cref="ISourceObjectCatalog"/> over in-memory tables, views and columns, for tests without a database. The managed-table
/// double changes it as the DDL it records would.
/// </summary>
internal sealed class FixedSourceObjectCatalog : ISourceObjectCatalog
{
    /// <summary>The tables and views.</summary>
    private readonly List<SourceObject> _objects = [];

    /// <summary>The columns of each table or view, by name.</summary>
    private readonly Dictionary<string, List<PhysicalColumn>> _columns = new(StringComparer.Ordinal);

    /// <summary>The values each column or JSON path holds, by object, column and path.</summary>
    public Dictionary<(string ObjectName, string ColumnName, string? JsonPath), IReadOnlyList<string>> Values { get; } = [];

    /// <summary>The statistics StarRocks reports, by name.</summary>
    public Dictionary<string, DatasetSourceObjectStats> Statistics { get; } = new(StringComparer.Ordinal);

    /// <summary>Adds a table or view with its columns, each given as a name and an information_schema type.</summary>
    /// <param name="kind">A <c>source_object_kind</c> value.</param>
    /// <param name="name">The object's name.</param>
    /// <param name="columns">Its columns.</param>
    /// <returns>The catalog.</returns>
    public FixedSourceObjectCatalog With(string kind, string name, params (string Name, string Type)[] columns)
    {
        _objects.Add(new SourceObject { Name = name, Kind = kind });
        _columns[name] = [.. columns.Select(column => new PhysicalColumn { Name = column.Name, ColumnType = column.Type, IsNullable = true, IsGenerated = false })];
        return this;
    }

    /// <summary>Adds a column to an object.</summary>
    /// <param name="objectName">The object.</param>
    /// <param name="column">The column.</param>
    public void AddColumn(string objectName, PhysicalColumn column)
    {
        _columns[objectName].Add(column);
    }

    /// <summary>Renames an object.</summary>
    /// <param name="name">The object.</param>
    /// <param name="newName">Its new name.</param>
    public void Rename(string name, string newName)
    {
        var index = _objects.FindIndex(item => item.Name == name);
        _objects[index] = _objects[index] with { Name = newName };
        _columns[newName] = _columns[name];
        _columns.Remove(name);
    }

    /// <summary>Removes an object with its columns.</summary>
    /// <param name="name">The object.</param>
    public void Remove(string name)
    {
        _objects.RemoveAll(item => item.Name == name);
        _columns.Remove(name);
    }

    /// <summary>Returns the tables and views.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The tables and views.</returns>
    public Task<IReadOnlyList<SourceObject>> ListObjectsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<SourceObject>>([.. _objects]);
    }

    /// <summary>Returns the columns of one object, or none for an unknown name.</summary>
    /// <param name="objectName">The object.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The columns.</returns>
    public Task<IReadOnlyList<PhysicalColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<PhysicalColumn>>(_columns.TryGetValue(objectName, out var columns) ? [.. columns] : []);
    }

    /// <summary>Returns the columns of every object.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The columns by object name.</returns>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>>> ListAllColumnsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>>>(
            _columns.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<PhysicalColumn>)[.. entry.Value], StringComparer.Ordinal));
    }

    /// <summary>Returns the statistics.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The statistics by object name.</returns>
    public Task<IReadOnlyDictionary<string, DatasetSourceObjectStats>> ListStatisticsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyDictionary<string, DatasetSourceObjectStats>>(new Dictionary<string, DatasetSourceObjectStats>(Statistics));
    }

    /// <summary>Returns the distinct values of a column or JSON path in ordinal order, up to the limit; none for an unknown one.</summary>
    /// <param name="source">The object, column and path.</param>
    /// <param name="limit">The most values to answer.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The values.</returns>
    public Task<FieldValues> ListDistinctValuesAsync(FieldValueSource source, int limit, CancellationToken cancellationToken)
    {
        string[] values = [.. Values.GetValueOrDefault((source.ObjectName, source.ColumnName, source.JsonPath), []).Distinct().Order(StringComparer.Ordinal)];
        return Task.FromResult(new FieldValues { Values = [.. values.Take(limit)], IsComplete = values.Length <= limit });
    }
}
