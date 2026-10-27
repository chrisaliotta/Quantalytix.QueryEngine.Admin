namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// One aggregate the change-set protocol edits (blueprint 01, OI-01), such as a dimension version's content. A module implements
/// it and registers it with <see cref="ChangesServiceCollectionExtensions.AddChangeSetAggregate{TAggregate, TSnapshot, TOperation, TState}"/>;
/// <see cref="ChangeSetPipeline{TSnapshot, TOperation, TState}"/> runs it. The snapshot is an immutable, in-memory copy of every
/// row the aggregate owns, so operations, rules and the write plan all work on values.
/// </summary>
/// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
/// <typeparam name="TOperation">The aggregate's operation union.</typeparam>
/// <typeparam name="TState">The content read model the editor renders.</typeparam>
public interface IChangeSetAggregate<TSnapshot, in TOperation, TState>
{
    /// <summary>Reads the aggregate as stored.</summary>
    /// <param name="aggregateId">The aggregate root's id, such as the dimension version id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    Task<TSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies one operation in memory. An operation that cannot apply calls <see cref="ChangeContext.Reject"/> or
    /// <see cref="ChangeContext.Conflict"/> and returns the snapshot it was given; the pipeline then stops.
    /// </summary>
    /// <param name="snapshot">The aggregate as the earlier operations left it.</param>
    /// <param name="operation">The operation, whose input its validator has passed.</param>
    /// <param name="context">Resolves the operation's ids and takes its refusals.</param>
    /// <returns>The aggregate with the operation applied.</returns>
    TSnapshot Apply(TSnapshot snapshot, TOperation operation, ChangeContext context);

    /// <summary>Adds the row writes that turn the stored aggregate into the changed one, each at its depth below the root.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan to add the writes to; it orders them.</param>
    void Plan(TSnapshot before, TSnapshot after, WritePlan plan);

    /// <summary>Whether the engine reads the change, so the commit advances the semantic revision (blueprint 01, Semantic revision).</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <returns>True when the commit must advance the revision.</returns>
    bool IsEngineVisible(TSnapshot before, TSnapshot after);

    /// <summary>The read model of a snapshot: the pending content for a preview, the stored content after a commit.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="cancellationToken">Cancels any reads the read model needs beyond the snapshot.</param>
    /// <returns>The read model.</returns>
    Task<TState> ReadAsync(TSnapshot snapshot, CancellationToken cancellationToken);
}
