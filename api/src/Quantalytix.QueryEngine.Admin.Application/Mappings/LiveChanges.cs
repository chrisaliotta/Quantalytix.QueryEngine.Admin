using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The commit of a mapping set or bridge change set. Both edit their live version in place, which live queries read at once, so a
/// commit is refused while any finding remains (B11): the commit previews the operations first and writes only a clean result.
/// </summary>
public static class LiveChanges
{
    /// <summary>Previews the operations, then commits them when the result has no findings.</summary>
    /// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
    /// <typeparam name="TOperation">The aggregate's operation union.</typeparam>
    /// <typeparam name="TState">The content read model.</typeparam>
    /// <param name="pipeline">Runs the aggregate.</param>
    /// <param name="aggregateId">The mapping set or bridge.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored state and id map, or the refusal (422 with the findings) with nothing written.</returns>
    public static async Task<Outcome<ChangeSetCommit<TState>>> CommitAsync<TSnapshot, TOperation, TState>(
        ChangeSetPipeline<TSnapshot, TOperation, TState> pipeline,
        long aggregateId,
        ChangeSetRequest<TOperation> request,
        CancellationToken cancellationToken)
        where TOperation : notnull
        where TState : notnull
    {
        var preview = await pipeline.PreviewAsync(aggregateId, request, cancellationToken);
        if (!preview.Succeeded) return Outcome<ChangeSetCommit<TState>>.Refused(preview.Refusal);
        if (preview.Value.Violations.Count > 0)
            return Outcome<ChangeSetCommit<TState>>.Refused(Refusal.RulesViolated("The live version would break rules; nothing was written.", preview.Value.Violations));
        return await pipeline.CommitAsync(aggregateId, request, cancellationToken);
    }
}
