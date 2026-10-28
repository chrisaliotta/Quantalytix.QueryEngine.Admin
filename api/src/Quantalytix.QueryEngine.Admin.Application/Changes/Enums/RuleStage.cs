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
    Publication,

    /// <summary>
    /// A commit rule, for aggregates whose saved content is live at once (mapping sets and bridges, B11): a preview reports its
    /// violations as findings, like a publication rule, so the editor can flag them, and a commit whose result breaks it is refused
    /// with 422 and writes nothing.
    /// </summary>
    Commit
}
