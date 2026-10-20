using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of calculation model versions, held in <c>qe_calculation_model_version</c>.</summary>
public sealed class CalculationModelVersionStore(ITableGateway<CalculationModelVersionRecord> gateway)
    : LivingVersionStore<CalculationModelVersionRecord>(gateway, VersionKinds.CalculationModelVersion)
{
    /// <summary>The gateway key of a model version.</summary>
    /// <param name="id">The model version ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { ModelVersionId = id };
    }

    /// <summary>The state a model version row describes.</summary>
    /// <param name="record">The model version row.</param>
    /// <returns>The version's state.</returns>
    protected override VersionState ToState(CalculationModelVersionRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.ModelVersionId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>The calculation model a version row belongs to.</summary>
    /// <param name="record">The version row.</param>
    /// <returns>The calculation model ID.</returns>
    protected override long IdentityOf(CalculationModelVersionRecord record)
    {
        return record.ModelId;
    }

    /// <summary>The gateway filter for a calculation model's published versions.</summary>
    /// <param name="identityId">The calculation model ID.</param>
    /// <returns>The filter object.</returns>
    protected override object PublishedOf(long identityId)
    {
        return new { ModelId = identityId, Status = PublicationStatuses.Published };
    }

    /// <summary>A copy of a model version row with a new status and publication time.</summary>
    /// <param name="record">The model version row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override CalculationModelVersionRecord WithStatus(CalculationModelVersionRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
