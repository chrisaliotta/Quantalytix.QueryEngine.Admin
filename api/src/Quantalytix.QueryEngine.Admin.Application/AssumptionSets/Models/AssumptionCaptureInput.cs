namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>A historical set to capture from the active set's current pins, as <c>POST assumption-sets</c> takes it.</summary>
public sealed record AssumptionCaptureInput
{
    /// <summary>The key requests will name the set by; unique and fixed once captured.</summary>
    public required string AssumptionSetKey { get; init; }

    /// <summary>The human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note, such as the close the snapshot records.</summary>
    public string? Description { get; init; }
}
