using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping set change set (OI-01) behind <c>mapping-sets/{mappingSetId}/changes</c>: the preview flags the entries that break a
/// rule, and the commit is refused while any finding remains (B11).
/// </summary>
/// <param name="pipeline">Runs the mapping set aggregate.</param>
public sealed class MappingSetChanges(ChangeSetPipeline<MappingSetSnapshot, MappingOperation, MappingSetEditState> pipeline)
{
    /// <summary>Applies the operations in memory and writes nothing.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending state and findings, or the refusal.</returns>
    public Task<Outcome<ChangeSetPreview<MappingSetEditState>>> PreviewAsync(
        long mappingSetId,
        ChangeSetRequest<MappingOperation> request,
        CancellationToken cancellationToken)
    {
        return pipeline.PreviewAsync(mappingSetId, request, cancellationToken);
    }

    /// <summary>Applies the operations to the mapping set as stored now and writes the result when it breaks no rule.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored state and id map, or the refusal with nothing written.</returns>
    public Task<Outcome<ChangeSetCommit<MappingSetEditState>>> CommitAsync(
        long mappingSetId,
        ChangeSetRequest<MappingOperation> request,
        CancellationToken cancellationToken)
    {
        return pipeline.CommitAsync(mappingSetId, request, cancellationToken);
    }
}
