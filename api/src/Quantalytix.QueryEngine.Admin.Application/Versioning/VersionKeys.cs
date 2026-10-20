using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Names the version and row a content route edits, and answers whether a key is free in that version; an edit may keep its own row's key.</summary>
public sealed class VersionKeys(IRequestRoute route)
{
    /// <summary>The model or report version the route names.</summary>
    public long VersionId => route.Id("versionId");

    /// <summary>The row the route edits, or null on a create route.</summary>
    public long? ItemId => route.OptionalId("itemId");

    /// <summary>Whether no other row of the version's table has the key.</summary>
    /// <typeparam name="TRecord">A content row.</typeparam>
    /// <param name="rows">The content table.</param>
    /// <param name="filter">The version and key to match, such as <c>new { ReportVersionId = id, SourceKey = key }</c>.</param>
    /// <param name="idOf">The ID of a row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the key is free.</returns>
    public async Task<bool> KeyFreeAsync<TRecord>(ITableGateway<TRecord> rows, object filter, Func<TRecord, long> idOf, CancellationToken cancellationToken)
        where TRecord : class
    {
        return (await rows.ListAsync(filter, cancellationToken)).All(row => idOf(row) == ItemId);
    }
}
