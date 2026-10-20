namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_dimension_binding</c> row: how a field of a dataset version classifies rows into a canonical dimension. A direct,
/// mapped, range or bridge binding binds the field to a role; a time binding names its dimension.
/// </summary>
public sealed record DimensionBindingRecord
{
    /// <summary>Primary key.</summary>
    public required long BindingId { get; init; }

    /// <summary>The dataset version exposing the dimension.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>The role the field is bound to, whose key requests name the binding by; null for a time binding.</summary>
    public long? RoleId { get; init; }

    /// <summary>The canonical dimension: the role's dimension, or the time binding's.</summary>
    public required long DimensionId { get; init; }

    /// <summary>A <c>binding_kind</c> value.</summary>
    public required string BindingKind { get; init; }

    /// <summary>The field whose values the binding classifies.</summary>
    public long? SourceFieldId { get; init; }

    /// <summary>The source vocabulary when the binding kind is <c>mapped</c>.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The existing source binding when the binding kind is <c>bridge</c>.</summary>
    public long? SourceBindingId { get; init; }

    /// <summary>The bridge when the binding kind is <c>bridge</c>; queries use the version their assumption set pins.</summary>
    public long? BridgeId { get; init; }

    /// <summary>A <c>ragged_behavior</c> value: what a row whose member lacks a requested ancestor groups under.</summary>
    public required string RaggedBehavior { get; init; }

    /// <summary>A <c>default_period_alignment</c> value.</summary>
    public required string DefaultPeriodAlignment { get; init; }

    /// <summary>An <c>unmapped_behavior</c> value.</summary>
    public required string UnmappedBehavior { get; init; }

    /// <summary>The key of the member unmapped rows route to when the unmapped behavior is <c>unknown</c>.</summary>
    public string? UnknownMemberKey { get; init; }

    /// <summary>Dataset fixture flag; a required binding must never be removed or disabled.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Whether the binding may take part in aggregate grouping.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether the dimension may take part in dimension-aware filters.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
