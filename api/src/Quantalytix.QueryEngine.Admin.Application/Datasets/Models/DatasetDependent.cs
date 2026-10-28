namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetDependent</c>: an item of the version that reads another item (OI-35).</summary>
public sealed record DatasetDependent
{
    /// <summary>The <see cref="DatasetTabs"/> value the dependent lives on.</summary>
    public required string Tab { get; init; }

    /// <summary>The dependent's id; null for the settings tab.</summary>
    public required string? ItemId { get; init; }

    /// <summary>The dependent's key, or <c>Settings</c>.</summary>
    public required string ItemKey { get; init; }

    /// <summary>How the dependent reads the item, such as <c>grain component</c>.</summary>
    public required string Relation { get; init; }

    /// <summary>Whether the dependent names the item by id, so deleting the item is refused.</summary>
    public required bool BlocksDelete { get; init; }
}
