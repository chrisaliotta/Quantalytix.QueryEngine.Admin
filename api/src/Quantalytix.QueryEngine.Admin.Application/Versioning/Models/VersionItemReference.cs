namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>A version content route: the model or report version and one of its rows.</summary>
/// <typeparam name="TItem">The kind of content row, which selects the validator that checks the row is the version's.</typeparam>
public sealed record VersionItemReference<TItem>
{
    /// <summary>The version that owns the row.</summary>
    public required long VersionId { get; init; }

    /// <summary>The row.</summary>
    public required long ItemId { get; init; }
}
