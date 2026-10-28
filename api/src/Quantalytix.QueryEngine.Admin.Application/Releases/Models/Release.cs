using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>Release</c>: one row of <c>qe_semantic_release</c>; screens show its key as its name (OI-39).</summary>
public record Release
{
    /// <summary>A release as stored.</summary>
    /// <param name="row">The release row.</param>
    [SetsRequiredMembers]
    public Release(SemanticReleaseRecord row)
    {
        Id = row.SemanticReleaseId;
        Key = row.ReleaseKey;
        Status = row.Status;
        CreatedAt = row.CreatedAt;
        PublishedAt = row.PublishedAt;
        ContentHash = row.ContentHash;
    }

    /// <summary>The release.</summary>
    public required long Id { get; init; }

    /// <summary>The natural key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Publication time (UTC); null until published.</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>Hash of the sorted manifest, computed on publish (OI-41); null until published.</summary>
    public required string? ContentHash { get; init; }
}
