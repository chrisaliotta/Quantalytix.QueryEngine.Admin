using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetListItem</c>: the identity with its live version, its versions in every snapshot and its storage.</summary>
public sealed record DatasetListItem : Dataset
{
    /// <summary>A dataset with what the list shows of it.</summary>
    /// <param name="dataset">The identity with the live version's enabled flag.</param>
    /// <param name="versions">Its versions, the live one first.</param>
    /// <param name="liveVersionId">The live version.</param>
    /// <param name="storage">The live version's source object statistics, or null.</param>
    [SetsRequiredMembers]
    public DatasetListItem(Dataset dataset, IReadOnlyList<DatasetVersionSummary> versions, string liveVersionId, DatasetSourceObjectStats? storage)
        : base(dataset)
    {
        Versions = versions;
        LiveVersionId = liveVersionId;
        Storage = storage;
    }

    /// <summary>The versions, one per snapshot holding the dataset: the live one first, then frozen ones, newest first.</summary>
    public required IReadOnlyList<DatasetVersionSummary> Versions { get; init; }

    /// <summary>The live version, which change sets edit and requests that name no snapshot read.</summary>
    public required string LiveVersionId { get; init; }

    /// <summary>Statistics of the live version's source object (OI-34).</summary>
    public required DatasetSourceObjectStats? Storage { get; init; }
}
