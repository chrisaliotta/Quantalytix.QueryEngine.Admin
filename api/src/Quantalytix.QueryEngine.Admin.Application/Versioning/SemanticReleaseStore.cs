using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>The lifecycle state of semantic releases, held in <c>qe_semantic_release</c>.</summary>
public sealed class SemanticReleaseStore(ITableGateway<SemanticReleaseRecord> gateway)
    : VersionStore<SemanticReleaseRecord>(gateway, VersionKinds.SemanticRelease)
{
    /// <summary>The gateway key of a semantic release.</summary>
    /// <param name="id">The release ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long id)
    {
        return new { SemanticReleaseId = id };
    }

    /// <summary>The state a semantic release row describes.</summary>
    /// <param name="record">The release row.</param>
    /// <returns>The release's state.</returns>
    protected override VersionState ToState(SemanticReleaseRecord record)
    {
        return new VersionState { Kind = Kind, Id = record.SemanticReleaseId, Status = record.Status, PublishedAt = record.PublishedAt };
    }

    /// <summary>A copy of a semantic release row with a new status and publication time.</summary>
    /// <param name="record">The release row.</param>
    /// <param name="status">The new status.</param>
    /// <param name="publishedAt">The publication time (UTC) to store.</param>
    /// <returns>The changed row.</returns>
    protected override SemanticReleaseRecord WithStatus(SemanticReleaseRecord record, string status, DateTime? publishedAt)
    {
        return record with { Status = status, PublishedAt = publishedAt };
    }
}
