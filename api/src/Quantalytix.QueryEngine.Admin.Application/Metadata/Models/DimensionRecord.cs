namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension</c> row: the stable identity of a canonical business dimension.</summary>
public sealed record DimensionRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long DimensionId { get; init; }

    /// <summary>Stable canonical identity such as <c>Branch</c> or <c>RiskGrade</c>.</summary>
    public required string DimensionKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>dimension_kind</c> value.</summary>
    public required string DimensionKind { get; init; }

    /// <summary>A <c>definition_origin</c> value.</summary>
    public required string DefinitionOrigin { get; init; }

    /// <summary>When false, the dimension must never be deleted.</summary>
    public required bool IsRemovable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>A <c>dimension_purpose</c> value: <c>report</c> for the dimension whose members are one report's rows.</summary>
    public required string DimensionPurpose { get; init; }

    /// <summary>The code-registry profile of a system dimension, such as <c>branch</c>; null for a user or report dimension.</summary>
    public string? DimensionProfileKey { get; init; }
}
