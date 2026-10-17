namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A model content route: the model version and one of its rows.</summary>
/// <typeparam name="TItem">The kind of content row, which selects the validator that checks the row is the version's.</typeparam>
public sealed record ModelItemReference<TItem>
{
    /// <summary>The model version that owns the row.</summary>
    public required long VersionId { get; init; }

    /// <summary>The row.</summary>
    public required long ItemId { get; init; }
}
