namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>The contract's <c>DatasetBindingDefinition</c>: the writable content of a binding (<c>qe_dimension_binding</c>) and its time axis.</summary>
public abstract record DatasetBindingDefinition
{
    /// <summary>A <c>binding_kind</c> value.</summary>
    public required string BindingKind { get; init; }

    /// <summary>The role a non-time binding binds; null for a time binding.</summary>
    public required string? DimensionRoleId { get; init; }

    /// <summary>The time dimension of a time binding; null for the others, which bind their role's dimension.</summary>
    public required string? DimensionId { get; init; }

    /// <summary>The field the binding classifies, stored or temporary.</summary>
    public required string? SourceFieldId { get; init; }

    /// <summary>The mapping set of a mapped binding.</summary>
    public required string? MappingSetId { get; init; }

    /// <summary>The bridge of a bridge binding.</summary>
    public required string? BridgeId { get; init; }

    /// <summary>The binding a bridge binding remaps, stored or temporary.</summary>
    public required string? SourceDimensionBindingId { get; init; }

    /// <summary>A <c>ragged_behavior</c> value.</summary>
    public required string RaggedBehavior { get; init; }

    /// <summary>A <c>period_alignment</c> value.</summary>
    public required string DefaultPeriodAlignment { get; init; }

    /// <summary>An <c>unmapped_behavior</c> value.</summary>
    public required string UnmappedBehavior { get; init; }

    /// <summary>The member unmapped rows route to under the <c>unknown</c> behavior.</summary>
    public required string? UnknownMemberKey { get; init; }

    /// <summary>Set by the server; a required binding is never disabled or deleted.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Whether the binding may take part in aggregate grouping.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether the binding may take part in dimension-aware filters.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The time axis of a time binding; null for the others.</summary>
    public required DatasetTimeBinding? Time { get; init; }
}
