namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>When a snapshot rule's violations block.</summary>
public enum RuleStage
{
    /// <summary>A gate: a change set whose result breaks it is refused with 422 and writes nothing, in preview and commit alike.</summary>
    Gate,

    /// <summary>
    /// A publication rule: it blocks validate, publish and freeze; a change set's preview reports its violations as findings that
    /// do not block saving.
    /// </summary>
    Publication
}
