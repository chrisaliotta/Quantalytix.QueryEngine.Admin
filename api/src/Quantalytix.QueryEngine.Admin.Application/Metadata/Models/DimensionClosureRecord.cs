namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_closure</c> row: a derived ancestor/descendant pair, rebuilt from member parentage and never edited directly.</summary>
public sealed record DimensionClosureRecord
{
    /// <summary>The dimension version; first part of the primary key.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>The ancestor member; second part of the primary key.</summary>
    public required long AncestorDimensionMemberId { get; init; }

    /// <summary>The descendant member; third part of the primary key.</summary>
    public required long DescendantDimensionMemberId { get; init; }

    /// <summary>Edge distance; self rows use <c>0</c>.</summary>
    public required int Depth { get; init; }
}
