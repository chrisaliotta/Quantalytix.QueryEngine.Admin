using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Content;

/// <summary>
/// Names the owner and row a content route edits, and answers whether a key is free among the owner's rows; an edit may keep its
/// own row's key.
/// </summary>
public sealed class ContentKeys(IRequestRoute route)
{
    /// <summary>The model version a model content route names.</summary>
    public long VersionId => route.Id("versionId");

    /// <summary>The report a report content route names.</summary>
    public long ReportId => route.Id("reportId");

    /// <summary>The report source a source route names.</summary>
    public long SourceId => route.Id("sourceId");

    /// <summary>The row the route edits, or null on a create route.</summary>
    public long? ItemId => route.OptionalId("itemId");

    /// <summary>Whether no other row of the owner's table has the key.</summary>
    /// <typeparam name="TRecord">A content row.</typeparam>
    /// <param name="rows">The content table.</param>
    /// <param name="filter">The owner and key to match, such as <c>new { ReportId = id, ProfileKey = key }</c>.</param>
    /// <param name="idOf">The ID of a row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the key is free.</returns>
    public async Task<bool> KeyFreeAsync<TRecord>(ITableGateway<TRecord> rows, object filter, Func<TRecord, long> idOf, CancellationToken cancellationToken)
        where TRecord : class
    {
        return (await rows.ListAsync(filter, cancellationToken)).All(row => idOf(row) == ItemId);
    }
}
