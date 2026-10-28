namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>CaptureSetInput</c>: the key and labels of a historical set to capture.</summary>
public sealed record CaptureSetInput
{
    /// <summary>The key requests will name the set by; unique and fixed (R11).</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note, such as the close the capture records.</summary>
    public required string? Description { get; init; }
}

/// <summary>The contract's <c>RelabelSetInput</c>: a historical set's new labels.</summary>
public sealed record RelabelSetInput
{
    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note.</summary>
    public required string? Description { get; init; }
}
