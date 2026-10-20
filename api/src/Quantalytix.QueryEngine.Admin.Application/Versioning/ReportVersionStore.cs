using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of report versions, held in <c>qe_report_version</c>.</summary>
public sealed class ReportVersionStore(ITableGateway<ReportVersionRecord> gateway)
    : LivingVersionStore<ReportVersionRecord>(gateway, VersionKinds.ReportVersion)
{
    /// <summary>The gateway key of a report version.</summary>
    /// <param name="id">The report version ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { ReportVersionId = id };
    }

    /// <summary>The state a report version row describes.</summary>
    /// <param name="record">The report version row.</param>
    /// <returns>The version's state.</returns>
    protected override VersionState ToState(ReportVersionRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.ReportVersionId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>The report a version row belongs to.</summary>
    /// <param name="record">The version row.</param>
    /// <returns>The report ID.</returns>
    protected override long IdentityOf(ReportVersionRecord record)
    {
        return record.ReportId;
    }

    /// <summary>The gateway filter for a report's published versions.</summary>
    /// <param name="identityId">The report ID.</param>
    /// <returns>The filter object.</returns>
    protected override object PublishedOf(long identityId)
    {
        return new { ReportId = identityId, Status = PublicationStatuses.Published };
    }

    /// <summary>A copy of a report version row with a new status and publication time.</summary>
    /// <param name="record">The report version row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override ReportVersionRecord WithStatus(ReportVersionRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
