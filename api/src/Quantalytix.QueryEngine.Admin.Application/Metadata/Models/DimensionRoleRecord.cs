namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_dimension_role</c> row: a named use of a dimension, its template, such as Servicing branch of Branch. Bindings,
/// calculation dimensions, bridge sources and report scenarios name it, and requests name its key.
/// </summary>
public sealed record DimensionRoleRecord
{
    /// <summary>Primary key.</summary>
    public required long DimensionRoleId { get; init; }

    /// <summary>The template dimension, fixed once created.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The dimension ID requests use, unique in the tenant and fixed once created.</summary>
    public required string DimensionRoleKey { get; init; }

    /// <summary>Human-readable name the catalog shows.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional description the catalog shows.</summary>
    public string? Description { get; init; }
}
