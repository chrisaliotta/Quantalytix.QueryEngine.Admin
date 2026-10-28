namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>Dataset</c>: the identity (<c>qe_dataset</c>) every version shares.</summary>
public record Dataset
{
    /// <summary>The dataset's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dataset key, fixed after creation (OI-36).</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether queries may read the dataset.</summary>
    public required bool IsEnabled { get; init; }
}
