using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetListItem</c>: the identity with its versions, the active release's pin and its storage.</summary>
public sealed record DatasetListItem : Dataset
{
    /// <summary>A dataset with what the list shows of it.</summary>
    /// <param name="dataset">The identity.</param>
    /// <param name="versions">Its versions, newest first.</param>
    /// <param name="activeVersionId">The version the active release pins, or null.</param>
    /// <param name="storage">The latest version's source object statistics, or null.</param>
    [SetsRequiredMembers]
    public DatasetListItem(Dataset dataset, IReadOnlyList<DatasetVersionSummary> versions, string? activeVersionId, DatasetSourceObjectStats? storage)
        : base(dataset)
    {
        Versions = versions;
        ActiveVersionId = activeVersionId;
        Storage = storage;
    }

    /// <summary>The versions, newest first.</summary>
    public required IReadOnlyList<DatasetVersionSummary> Versions { get; init; }

    /// <summary>The version the active release pins; what the engine reads.</summary>
    public required string? ActiveVersionId { get; init; }

    /// <summary>Statistics of the latest version's source object (OI-34).</summary>
    public required DatasetSourceObjectStats? Storage { get; init; }
}
