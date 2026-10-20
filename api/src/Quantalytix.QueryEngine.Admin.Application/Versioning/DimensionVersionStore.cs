using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of dimension versions, held in <c>qe_dimension_version</c>.</summary>
public sealed class DimensionVersionStore(ITableGateway<DimensionVersionRecord> gateway)
    : LivingVersionStore<DimensionVersionRecord>(gateway, VersionKinds.DimensionVersion)
{
    /// <summary>The gateway key of a dimension version.</summary>
    /// <param name="id">The dimension version ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { DimensionVersionId = id };
    }

    /// <summary>The state a dimension version row describes.</summary>
    /// <param name="record">The dimension version row.</param>
    /// <returns>The version's state.</returns>
    protected override VersionState ToState(DimensionVersionRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.DimensionVersionId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>The dimension a version row belongs to.</summary>
    /// <param name="record">The version row.</param>
    /// <returns>The dimension ID.</returns>
    protected override long IdentityOf(DimensionVersionRecord record)
    {
        return record.DimensionId;
    }

    /// <summary>The gateway filter for a dimension's published versions.</summary>
    /// <param name="identityId">The dimension ID.</param>
    /// <returns>The filter object.</returns>
    protected override object PublishedOf(long identityId)
    {
        return new { DimensionId = identityId, Status = PublicationStatuses.Published };
    }

    /// <summary>A copy of a dimension version row with a new status and publication time.</summary>
    /// <param name="record">The dimension version row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override DimensionVersionRecord WithStatus(DimensionVersionRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
