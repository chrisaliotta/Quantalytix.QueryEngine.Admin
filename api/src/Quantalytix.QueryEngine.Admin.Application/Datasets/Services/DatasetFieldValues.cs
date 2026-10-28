using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The datasets module's public read of the values a field holds in its version's source object, for consumers that compare data
/// with keys, such as a report's direct sources with its rows. The read is bounded to <see cref="Limit"/> values and the catalog keeps
/// it briefly. An expression field's values exist only as the engine computes them, so it has none to read.
/// </summary>
/// <param name="versions">Reads the field's version, which names the source object.</param>
/// <param name="catalog">Reads the values.</param>
public sealed class DatasetFieldValues(ITableGateway<DatasetVersionRecord> versions, ISourceObjectCatalog catalog)
{
    /// <summary>The most values one read answers.</summary>
    public const int Limit = 1000;

    /// <summary>The distinct non-null values of a column or JSON path field.</summary>
    /// <param name="field">A field of a dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The values as text in ordinal order; null for an expression field.</returns>
    public async Task<FieldValues?> ReadAsync(FieldRecord field, CancellationToken cancellationToken)
    {
        if (field.FieldSourceKind == FieldSourceKinds.Expression) return null;
        var version = await versions.GetAsync(new { field.DatasetVersionId }, cancellationToken);
        var source = new FieldValueSource { ObjectName = version.SourceObject, ColumnName = field.SourceColumn!, JsonPath = field.JsonPath };
        return await catalog.ListDistinctValuesAsync(source, Limit, cancellationToken);
    }
}
