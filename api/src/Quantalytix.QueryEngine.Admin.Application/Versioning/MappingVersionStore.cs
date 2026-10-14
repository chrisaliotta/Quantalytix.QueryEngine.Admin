using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of mapping versions, held in <c>qe_mapping_version</c>.</summary>
public sealed class MappingVersionStore(ITableGateway<MappingVersionRecord> gateway)
    : VersionStore<MappingVersionRecord>(gateway, VersionKinds.MappingVersion)
{
    /// <summary>The gateway key of a mapping version.</summary>
    /// <param name="id">The mapping version ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { MappingVersionId = id };
    }

    /// <summary>The state a mapping version row describes.</summary>
    /// <param name="record">The mapping version row.</param>
    /// <returns>The version's state.</returns>
    protected override VersionState ToState(MappingVersionRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.MappingVersionId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>A copy of a mapping version row with a new status and publication time.</summary>
    /// <param name="record">The mapping version row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override MappingVersionRecord WithStatus(MappingVersionRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
