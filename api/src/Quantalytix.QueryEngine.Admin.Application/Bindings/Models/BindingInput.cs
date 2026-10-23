namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// The attributes of a dimension binding of a draft dataset version, plus the time extension of a <c>time</c> binding.
/// Bindings have no key, so the same input creates a binding and replaces one. A direct, mapped, range or bridge binding
/// binds its field to a role, whose dimension it classifies into; a time binding names its time dimension.
/// </summary>
public sealed record BindingInput
{
    /// <summary>The role a direct, mapped, range or bridge binding binds its field to; null for a <c>time</c> binding.</summary>
    public long? DimensionRoleId { get; init; }

    /// <summary>The time dimension of a <c>time</c> binding; null for every other kind, which classifies into its role's dimension.</summary>
    public long? DimensionId { get; init; }

    /// <summary>A <c>binding_kind</c> value: <c>direct</c>, <c>mapped</c>, <c>range</c>, <c>bridge</c> or <c>time</c>.</summary>
    public required string BindingKind { get; init; }

    /// <summary>
    /// The field the binding reads. Required for <c>direct</c>, <c>mapped</c>, <c>range</c> and <c>time</c> bindings; optional
    /// for <c>bridge</c> bindings, which remap the members of their source binding.
    /// </summary>
    public long? SourceFieldId { get; init; }

    /// <summary>The source vocabulary of a <c>mapped</c> binding; null for every other kind.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The binding of the same version that a <c>bridge</c> binding remaps; null for every other kind.</summary>
    public long? SourceDimensionBindingId { get; init; }

    /// <summary>The bridge a <c>bridge</c> binding remaps through; null for every other kind. Queries use the version their assumption set pins.</summary>
    public long? BridgeId { get; init; }

    /// <summary>A <c>ragged_behavior</c> value: what a grouping does with a member that has no ancestor at the requested level.</summary>
    public required string RaggedBehavior { get; init; }

    /// <summary>A <c>default_period_alignment</c> value.</summary>
    public required string DefaultPeriodAlignment { get; init; }

    /// <summary>An <c>unmapped_behavior</c> value: what happens to rows whose source value maps to no member.</summary>
    public required string UnmappedBehavior { get; init; }

    /// <summary>The key of the member unmapped rows route to; required when the unmapped behavior is <c>unknown</c> and null otherwise.</summary>
    public string? UnknownMemberKey { get; init; }

    /// <summary>Whether the binding is a dataset fixture that must not be removed or disabled.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Whether the binding may take part in aggregate grouping.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether the dimension may take part in dimension-aware filters.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The time extension; required for <c>time</c> bindings and absent for every other kind.</summary>
    public TimeBindingInput? TimeBinding { get; init; }
}
