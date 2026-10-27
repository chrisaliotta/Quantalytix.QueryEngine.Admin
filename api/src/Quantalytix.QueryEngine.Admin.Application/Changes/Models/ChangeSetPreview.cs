using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>The 200 answer of <c>POST &lt;aggregate&gt;/changes/preview</c>: the operations apply; nothing was written.</summary>
/// <typeparam name="TState">The aggregate's content read model.</typeparam>
public sealed record ChangeSetPreview<TState>
    where TState : notnull
{
    /// <summary>The aggregate's content after the operations apply; what the editor renders.</summary>
    public required TState State { get; init; }

    /// <summary>Publication-rule findings that do not block saving.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
