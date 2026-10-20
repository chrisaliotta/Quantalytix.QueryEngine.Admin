namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The editable labels of an assumption set; its key and pins never change.</summary>
public sealed record AssumptionSetUpdateInput
{
    /// <summary>The human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note, such as the close the snapshot records.</summary>
    public string? Description { get; init; }
}
