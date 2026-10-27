namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>The body of <c>POST &lt;aggregate&gt;/changes/preview</c> and <c>POST &lt;aggregate&gt;/changes</c> (blueprint 01, OI-01).</summary>
/// <typeparam name="TOperation">The aggregate's operation union, discriminated by <c>op</c>.</typeparam>
public sealed record ChangeSetRequest<TOperation>
{
    /// <summary>The operations, applied in order.</summary>
    public required IReadOnlyList<TOperation> Operations { get; init; }
}
