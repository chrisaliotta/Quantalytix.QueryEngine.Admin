namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>The 200 answer of <c>POST &lt;aggregate&gt;/changes</c>: every operation applied and was written.</summary>
/// <typeparam name="TState">The aggregate's content read model.</typeparam>
public sealed record ChangeSetCommit<TState>
    where TState : notnull
{
    /// <summary>The aggregate's content as stored after the commit.</summary>
    public required TState State { get; init; }

    /// <summary>The real id of each item the commit created, by the temporary id (<c>tmp:&lt;uuid&gt;</c>) the client gave it.</summary>
    public required IReadOnlyDictionary<string, long> IdMap { get; init; }
}
