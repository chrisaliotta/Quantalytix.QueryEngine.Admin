namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingAssumptionPin</c>: an assumption set that pins one version of a mapping set or bridge.</summary>
public sealed record MappingAssumptionPin
{
    /// <summary>The assumption set.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>Its key.</summary>
    public required string AssumptionSetKey { get; init; }

    /// <summary>Its display name.</summary>
    public required string AssumptionSetDisplayName { get; init; }

    /// <summary>Whether it is the active set, which always pins the live version; a historical set keeps the version it captured.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The pinned version.</summary>
    public required long VersionId { get; init; }

    /// <summary>The pinned version's number.</summary>
    public required int VersionNumber { get; init; }
}
