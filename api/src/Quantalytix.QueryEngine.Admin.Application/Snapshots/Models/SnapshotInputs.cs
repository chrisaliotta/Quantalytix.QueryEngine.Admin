namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>The contract's <c>CaptureSnapshotInput</c>: the key and labels of a frozen snapshot to capture.</summary>
public sealed record CaptureSnapshotInput
{
    /// <summary>The key requests will name the snapshot by; unique and fixed.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note, such as the close the capture records.</summary>
    public required string? Description { get; init; }
}

/// <summary>The contract's <c>RelabelSnapshotInput</c>: a frozen snapshot's new labels.</summary>
public sealed record RelabelSnapshotInput
{
    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note.</summary>
    public required string? Description { get; init; }
}
