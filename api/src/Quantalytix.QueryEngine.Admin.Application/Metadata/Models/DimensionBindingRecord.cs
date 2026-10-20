namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_binding</c> row: a semantic classification route from a dataset field to a canonical dimension.</summary>
public sealed record DimensionBindingRecord
{
    /// <summary>Primary key.</summary>
    public required long BindingId { get; init; }

    /// <summary>The dataset version exposing the dimension.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>Human-readable role name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The canonical dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>A <c>binding_kind</c> value.</summary>
    public required string BindingKind { get; init; }

    /// <summary>The request-facing field for source-backed bindings.</summary>
    public long? SourceFieldId { get; init; }

    /// <summary>The source vocabulary when the binding kind is <c>mapped</c>.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The existing source binding when the binding kind is <c>bridge</c>.</summary>
    public long? SourceBindingId { get; init; }

    /// <summary>The bridge when the binding kind is <c>bridge</c>; queries use the version their assumption set pins.</summary>
    public long? BridgeId { get; init; }

    /// <summary>A <c>grouping_mode</c> value.</summary>
    public required string GroupingMode { get; init; }

    /// <summary>The key of the level to group at when the grouping mode is <c>level</c>.</summary>
    public string? GroupingLevelKey { get; init; }

    /// <summary>The key of the ancestor member to group under when the grouping mode is <c>ancestor</c>.</summary>
    public string? GroupingAncestorMemberKey { get; init; }

    /// <summary>The closure depth to group at when the grouping mode is <c>depth</c>.</summary>
    public int? GroupingDepth { get; init; }

    /// <summary>A <c>ragged_behavior</c> value.</summary>
    public required string RaggedBehavior { get; init; }

    /// <summary>A <c>default_period_alignment</c> value.</summary>
    public required string DefaultPeriodAlignment { get; init; }

    /// <summary>An <c>unmapped_behavior</c> value.</summary>
    public required string UnmappedBehavior { get; init; }

    /// <summary>The key of the member unmapped rows route to when the unmapped behavior is <c>unknown</c>.</summary>
    public string? UnknownMemberKey { get; init; }

    /// <summary>Dataset fixture flag; a required binding must never be removed or disabled.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Whether the semantic route may take part in aggregate grouping.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether the dimension may take part in dimension-aware filters.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
