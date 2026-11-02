namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>When a snapshot rule's violations block.</summary>
public enum RuleStage
{
    /// <summary>A gate: a change set whose result breaks it is refused with 422 and writes nothing, in preview and commit alike.</summary>
    Gate,

    /// <summary>
    /// A commit rule: saved content is live at once, so a preview reports its violations as findings the editor flags, and a commit
    /// whose result breaks it is refused with 422 and writes nothing. Restore runs it on every version it would make live.
    /// </summary>
    Commit
}
