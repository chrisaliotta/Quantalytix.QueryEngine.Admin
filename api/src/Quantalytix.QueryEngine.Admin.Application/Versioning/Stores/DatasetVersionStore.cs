using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of dataset versions, held in <c>qe_dataset_version</c>.</summary>
public sealed class DatasetVersionStore(ITableGateway<DatasetVersionRecord> gateway)
    : VersionStore<DatasetVersionRecord>(gateway, VersionKinds.DatasetVersion)
{
    /// <summary>The gateway key of a dataset version.</summary>
    /// <param name="id">The dataset version ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { DatasetVersionId = id };
    }

    /// <summary>The state a dataset version row describes.</summary>
    /// <param name="record">The dataset version row.</param>
    /// <returns>The version's state.</returns>
    protected override VersionState ToState(DatasetVersionRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.DatasetVersionId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>A copy of a version row with a new status, publication time and retirement time.</summary>
    /// <param name="record">The version row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <param name="retiredAt">The retirement time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override DatasetVersionRecord WithStatus(DatasetVersionRecord record, string status, DateTime? publishedAt, DateTime? retiredAt)
    {
        return record with { Status = status, PublishedAt = publishedAt, RetiredAt = retiredAt };
    }
}
