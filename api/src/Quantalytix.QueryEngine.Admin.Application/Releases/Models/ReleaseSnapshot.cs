using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A release aggregate in memory: the release row and its manifest, stored or pending, with what surrounds it.</summary>
public sealed record ReleaseSnapshot
{
    /// <summary>The release.</summary>
    public required SemanticReleaseRecord Release { get; init; }

    /// <summary>The manifest rows, by dataset.</summary>
    public required IReadOnlyList<ReleaseDatasetRecord> Manifest { get; init; }

    /// <summary>The tenant state the rules and the read model consult.</summary>
    public required ReleaseSurroundings Surroundings { get; init; }
}

/// <summary>What outside a release its manifest operation, rules and read model consult.</summary>
public sealed record ReleaseSurroundings
{
    /// <summary>Every dataset of the tenant.</summary>
    public required IReadOnlyList<DatasetRecord> Datasets { get; init; }

    /// <summary>Every dataset version of the tenant.</summary>
    public required IReadOnlyList<DatasetVersionRecord> DatasetVersions { get; init; }

    /// <summary>The tenant's active release; null when none is active.</summary>
    public required long? ActiveReleaseId { get; init; }

    /// <summary>The release's activations, newest first.</summary>
    public required IReadOnlyList<ReleaseActivationRecord> Activations { get; init; }
}
