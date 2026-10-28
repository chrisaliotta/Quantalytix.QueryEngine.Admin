using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ReleaseActivation</c>: one row of the activation log, <c>qe_release_activation</c> (OI-40).</summary>
public sealed record ReleaseActivation
{
    /// <summary>An activation as stored.</summary>
    /// <param name="row">The log row.</param>
    [SetsRequiredMembers]
    public ReleaseActivation(ReleaseActivationRecord row)
    {
        ReleaseId = row.SemanticReleaseId;
        PreviousReleaseId = row.PreviousSemanticReleaseId;
        ActivatedAt = row.ActivatedAt;
        ActivatedBy = row.ActivatedBy;
    }

    /// <summary>The release activated.</summary>
    public required long ReleaseId { get; init; }

    /// <summary>The release that was active before; null for the tenant's first activation.</summary>
    public required long? PreviousReleaseId { get; init; }

    /// <summary>Activation time (UTC).</summary>
    public required DateTime ActivatedAt { get; init; }

    /// <summary>The token subject of the caller.</summary>
    public required string ActivatedBy { get; init; }
}
