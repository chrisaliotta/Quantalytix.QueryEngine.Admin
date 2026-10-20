namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_dimension_role</c> row: a named use of a dimension, its template, such as Servicing branch of Branch. Bindings,
/// calculation dimensions and bridge sources name it, and requests name its key.
/// </summary>
public sealed record DimensionRoleRecord
{
    /// <summary>Primary key.</summary>
    public required long RoleId { get; init; }

    /// <summary>The template dimension, fixed once created.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The dimension ID requests use, unique in the tenant and fixed once created.</summary>
    public required string RoleKey { get; init; }

    /// <summary>Human-readable name the catalog shows.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional description the catalog shows.</summary>
    public string? Description { get; init; }
}
