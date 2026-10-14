using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of assumption sets, held in <c>qe_assumption_set</c>.</summary>
public sealed class AssumptionSetStore(ITableGateway<AssumptionSetRecord> gateway)
    : VersionStore<AssumptionSetRecord>(gateway, VersionKinds.AssumptionSet)
{
    /// <summary>The gateway key of an assumption set.</summary>
    /// <param name="id">The assumption set ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { AssumptionSetId = id };
    }

    /// <summary>The state an assumption set row describes.</summary>
    /// <param name="record">The assumption set row.</param>
    /// <returns>The assumption set's state.</returns>
    protected override VersionState ToState(AssumptionSetRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.AssumptionSetId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>A copy of an assumption set row with a new status and publication time.</summary>
    /// <param name="record">The assumption set row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override AssumptionSetRecord WithStatus(AssumptionSetRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
