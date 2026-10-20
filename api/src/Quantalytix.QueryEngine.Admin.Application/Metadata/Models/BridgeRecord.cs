namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_bridge</c> row: the stable identity of a semantic remapping from one canonical dimension to another.</summary>
public sealed record BridgeRecord
{
    /// <summary>Primary key.</summary>
    public required long BridgeId { get; init; }

    /// <summary>Stable logical bridge identity.</summary>
    public required string BridgeKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The canonical source dimension.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>The canonical target dimension.</summary>
    public required long TargetDimensionId { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
