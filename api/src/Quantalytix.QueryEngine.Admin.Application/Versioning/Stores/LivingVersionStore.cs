using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>A version store over the table of a living kind, which finds a version's identity and that identity's published versions.</summary>
/// <typeparam name="TRecord">The record of the versioned table.</typeparam>
/// <param name="gateway">The gateway of the versioned table.</param>
/// <param name="kind">The living <c>version_kind</c> value the store serves.</param>
public abstract class LivingVersionStore<TRecord>(ITableGateway<TRecord> gateway, string kind)
    : VersionStore<TRecord>(gateway, kind), ILivingVersionStore where TRecord : class
{
    /// <summary>The identity a version belongs to.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The identity's surrogate key.</returns>
    public async Task<long> IdentityIdAsync(long id, CancellationToken cancellationToken)
    {
        return IdentityOf(await Gateway.GetAsync(Key(id), cancellationToken));
    }

    /// <summary>The other published versions of the identity a version belongs to.</summary>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The sibling versions' surrogate keys.</returns>
    public async Task<IReadOnlyList<long>> PublishedSiblingIdsAsync(long id, CancellationToken cancellationToken)
    {
        var published = await Gateway.ListAsync(PublishedOf(await IdentityIdAsync(id, cancellationToken)), cancellationToken);
        return [.. published.Select(record => ToState(record).Id).Where(siblingId => siblingId != id)];
    }

    /// <summary>The identity a version row belongs to, such as its <c>DimensionId</c>.</summary>
    /// <param name="record">The version's row.</param>
    /// <returns>The identity's surrogate key.</returns>
    protected abstract long IdentityOf(TRecord record);

    /// <summary>The gateway filter for an identity's published versions, such as <c>new { DimensionId = identityId, Status = "published" }</c>.</summary>
    /// <param name="identityId">The identity's surrogate key.</param>
    /// <returns>An anonymous object of record property values to match.</returns>
    protected abstract object PublishedOf(long identityId);
}
