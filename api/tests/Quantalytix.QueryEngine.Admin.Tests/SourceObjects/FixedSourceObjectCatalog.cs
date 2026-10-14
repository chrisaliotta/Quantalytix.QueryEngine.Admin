using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

/// <summary>An <see cref="ISourceObjectCatalog"/> over fixed tables, views and columns, for tests without a database.</summary>
/// <param name="objects">Every table and view of the pretend tenant database.</param>
/// <param name="columns">The columns of each table or view, by name.</param>
internal sealed class FixedSourceObjectCatalog(
    IReadOnlyList<SourceObject> objects,
    IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>> columns) : ISourceObjectCatalog
{
    /// <summary>Returns the fixed tables and views.</summary>
    /// <param name="cancellationToken">Unused; the list answers synchronously.</param>
    /// <returns>The tables and views.</returns>
    public Task<IReadOnlyList<SourceObject>> ListObjectsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(objects);
    }

    /// <summary>Returns the fixed columns of one table or view, or none for an unknown name.</summary>
    /// <param name="objectName">The table or view name.</param>
    /// <param name="cancellationToken">Unused; the list answers synchronously.</param>
    /// <returns>The columns.</returns>
    public Task<IReadOnlyList<PhysicalColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken)
    {
        return Task.FromResult(columns.GetValueOrDefault(objectName, []));
    }
}
