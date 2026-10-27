namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionEditState</c>: the change-set aggregate as the dimension editor shows it.</summary>
public sealed record DimensionEditState
{
    /// <summary>The dimension with its roles, versions and usage.</summary>
    public required DimensionDetail Dimension { get; init; }

    /// <summary>The live version's content.</summary>
    public required DimensionVersionContent Content { get; init; }
}
