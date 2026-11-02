namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The contract's <c>MappingBindingUsage</c>: a dataset binding that names a mapping set or bridge by identity (B13), which the
/// Used by list shows and which blocks deleting the identity.
/// </summary>
public sealed record MappingBindingUsage
{
    /// <summary>The dataset.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The dataset key.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The dataset version that holds the binding, in the same snapshot as the identity's version.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>The field whose values the binding classifies; a bridge binding's is its source binding's.</summary>
    public required string FieldKey { get; init; }

    /// <summary>The key of the role the binding binds.</summary>
    public required string RoleKey { get; init; }

    /// <summary>Bridge bindings only: the role of the binding the bridge remaps; null for mapping sets.</summary>
    public required string? SourceRoleKey { get; init; }
}
