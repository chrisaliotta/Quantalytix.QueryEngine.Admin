namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The editable identity attributes of a dataset. <see cref="DatasetInput"/> adds the attributes fixed at creation.</summary>
public record DatasetUpdateInput
{
    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
