namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The contract's <c>DimensionListItem</c>: an analytical dimension with its profile, roles, version summaries and the member
/// count of its live version. Ids are written as the API presents them (<see cref="Changes.TemporaryIds.Present"/>).
/// </summary>
public record DimensionListItem
{
    /// <summary>The dimension's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dimension key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>dimension_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>A <c>definition_origin</c> value.</summary>
    public required string Origin { get; init; }

    /// <summary>A <c>dimension_purpose</c> value; always <c>analytical</c> in this module.</summary>
    public required string Purpose { get; init; }

    /// <summary>The report that owns a report dimension; null for every dimension this module answers.</summary>
    public required DimensionOwnerReport? OwnerReport { get; init; }

    /// <summary>The profile key of a system dimension; null for a user dimension.</summary>
    public required string? ProfileKey { get; init; }

    /// <summary>Whether the dimension is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Whether the dimension may be deleted.</summary>
    public required bool IsRemovable { get; init; }

    /// <summary>The profile of a system dimension; null for a user dimension.</summary>
    public required DimensionProfile? Profile { get; init; }

    /// <summary>The roles, by key.</summary>
    public required IReadOnlyList<DimensionRole> Roles { get; init; }

    /// <summary>Every version, by version number.</summary>
    public required IReadOnlyList<DimensionVersionSummary> Versions { get; init; }

    /// <summary>The members of the live version; null when the dimension has no live version.</summary>
    public required int? PublishedMemberCount { get; init; }
}
