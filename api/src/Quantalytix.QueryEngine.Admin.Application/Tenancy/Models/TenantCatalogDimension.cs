namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The contract's <c>TenantCatalogDimension</c>: an analytical or report dimension with its live version.</summary>
public sealed record TenantCatalogDimension
{
    /// <summary>The dimension's id.</summary>
    public required long Id { get; init; }

    /// <summary>The dimension key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>dimension_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>A <c>dimension_purpose</c> value.</summary>
    public required string Purpose { get; init; }

    /// <summary>A <c>definition_origin</c> value.</summary>
    public required string Origin { get; init; }

    /// <summary>Whether the dimension is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The live version; null when none is published.</summary>
    public required long? PublishedVersionId { get; init; }

    /// <summary>The live version's number; null when none is published.</summary>
    public required int? PublishedVersionNumber { get; init; }

    /// <summary>The report that owns a <c>report</c> purpose dimension; null otherwise.</summary>
    public required long? OwnerReportId { get; init; }
}
