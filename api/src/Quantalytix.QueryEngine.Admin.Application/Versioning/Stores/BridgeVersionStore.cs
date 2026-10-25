using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of bridge versions, held in <c>qe_bridge_version</c>.</summary>
public sealed class BridgeVersionStore(ITableGateway<BridgeVersionRecord> gateway)
    : LivingVersionStore<BridgeVersionRecord>(gateway, VersionKinds.BridgeVersion)
{
    /// <summary>The gateway key of a bridge version.</summary>
    /// <param name="id">The bridge version ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { BridgeVersionId = id };
    }

    /// <summary>The state a bridge version row describes.</summary>
    /// <param name="record">The bridge version row.</param>
    /// <returns>The version's state.</returns>
    protected override VersionState ToState(BridgeVersionRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.BridgeVersionId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>The bridge a version row belongs to.</summary>
    /// <param name="record">The version row.</param>
    /// <returns>The bridge ID.</returns>
    protected override long IdentityOf(BridgeVersionRecord record)
    {
        return record.BridgeId;
    }

    /// <summary>The gateway filter for a bridge's published versions.</summary>
    /// <param name="identityId">The bridge ID.</param>
    /// <returns>The filter object.</returns>
    protected override object PublishedOf(long identityId)
    {
        return new { BridgeId = identityId, Status = PublicationStatuses.Published };
    }

    /// <summary>A copy of a bridge version row with a new status and publication time.</summary>
    /// <param name="record">The bridge version row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override BridgeVersionRecord WithStatus(BridgeVersionRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
