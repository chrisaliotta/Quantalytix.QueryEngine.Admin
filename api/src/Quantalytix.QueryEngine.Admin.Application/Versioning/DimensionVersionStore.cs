using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of dimension versions, held in <c>qe_dimension_version</c>.</summary>
public sealed class DimensionVersionStore(ITableGateway<DimensionVersionRecord> gateway)
    : VersionStore<DimensionVersionRecord>(gateway, VersionKinds.DimensionVersion)
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
