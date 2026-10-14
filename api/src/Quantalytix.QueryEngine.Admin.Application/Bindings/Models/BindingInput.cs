namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// The attributes of a dimension binding of a draft dataset version, plus the time extension of a <c>time</c> binding.
/// Bindings have no key, so the same input creates a binding and replaces one.
/// </summary>
public sealed record BindingInput
{
    /// <summary>Human-readable role name, such as <c>Origination branch</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The canonical dimension the binding classifies its field into.</summary>
    public required long DimensionId { get; init; }

    /// <summary>An explicit version of the dimension; null resolves the published version effective at query time.</summary>
    public long? PinnedDimensionVersionId { get; init; }

    /// <summary>A <c>binding_kind</c> value: <c>direct</c>, <c>mapped</c>, <c>range</c>, <c>bridge</c> or <c>time</c>.</summary>
    public required string BindingKind { get; init; }

    /// <summary>
    /// The request-facing field the binding reads. Required for <c>direct</c>, <c>mapped</c>, <c>range</c> and <c>time</c>
    /// bindings; optional for <c>bridge</c> bindings, where it gives the binding a request-facing field key.
    /// </summary>
    public long? SourceFieldId { get; init; }

    /// <summary>The source vocabulary of a <c>mapped</c> binding; null for every other kind.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The binding of the same version that a <c>bridge</c> binding remaps; null for every other kind.</summary>
    public long? SourceBindingId { get; init; }

    /// <summary>The bridge version a <c>bridge</c> binding remaps through; null for every other kind.</summary>
    public long? BridgeVersionId { get; init; }

    /// <summary>A <c>grouping_mode</c> value: <c>member</c>, <c>level</c>, <c>ancestor</c> or <c>depth</c>.</summary>
    public required string GroupingMode { get; init; }

    /// <summary>The level to group at; required for <c>level</c> grouping and null otherwise.</summary>
    public long? GroupingLevelId { get; init; }

    /// <summary>The ancestor member to group at; required for <c>ancestor</c> grouping and null otherwise.</summary>
    public long? GroupingAncestorMemberId { get; init; }

    /// <summary>The closure depth to group at, 0 or more; required for <c>depth</c> grouping and null otherwise.</summary>
    public int? GroupingDepth { get; init; }

    /// <summary>A <c>ragged_behavior</c> value: what grouping does with a member that has no ancestor at the requested level.</summary>
    public required string RaggedBehavior { get; init; }

    /// <summary>A <c>default_period_alignment</c> value.</summary>
    public required string DefaultPeriodAlignment { get; init; }

    /// <summary>An <c>unmapped_behavior</c> value: what happens to rows whose source value maps to no member.</summary>
    public required string UnmappedBehavior { get; init; }

    /// <summary>The member unmapped rows route to; required when the unmapped behavior is <c>unknown</c> and null otherwise.</summary>
    public long? UnknownMemberId { get; init; }

    /// <summary>Whether the binding is a dataset fixture that must not be removed or disabled.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Whether the semantic route may take part in aggregate grouping.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether the dimension may take part in dimension-aware filters.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The time extension; required for <c>time</c> bindings and absent for every other kind.</summary>
    public TimeBindingInput? TimeBinding { get; init; }
}
