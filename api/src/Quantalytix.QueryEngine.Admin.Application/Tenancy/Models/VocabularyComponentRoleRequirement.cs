namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>One item of the contract's <c>VocabularyComponentRoleRequirements</c>: a component role a measure kind reads.</summary>
public sealed record VocabularyComponentRoleRequirement
{
    /// <summary>A <c>component_role</c> value.</summary>
    public required string Role { get; init; }

    /// <summary>Whether a measure of the kind must have a component in the role.</summary>
    public required bool Required { get; init; }
}
