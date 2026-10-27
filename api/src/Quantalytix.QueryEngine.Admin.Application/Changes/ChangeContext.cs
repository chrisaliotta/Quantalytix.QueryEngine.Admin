using System.Globalization;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// What an aggregate's <see cref="IChangeSetAggregate{TSnapshot, TOperation, TState}.Apply"/> sees of the change set it is
/// applying: the ids it resolves and the refusals it raises. Errors are keyed by the failing operation
/// (<c>operations[3].memberKey</c>), so the editor shows them on the edit that caused them.
/// </summary>
/// <param name="ids">The change set's id resolution.</param>
public sealed class ChangeContext(TemporaryIds ids)
{
    /// <summary>The errors of the operation being applied.</summary>
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    /// <summary>A refusal raised as a conflict.</summary>
    private Refusal? _conflict;

    /// <summary>The ids of the change set, stored or temporary.</summary>
    public TemporaryIds Ids { get; } = ids;

    /// <summary>The index of the operation being applied.</summary>
    public int OperationIndex { get; private set; }

    /// <summary>Refuses the operation's input (400); further errors of the same operation are collected with it.</summary>
    /// <param name="path">The input's camelCase path within the operation, such as <c>input.memberKey</c>; empty for the operation itself.</param>
    /// <param name="message">What is wrong.</param>
    public void Reject(string path, string message)
    {
        var key = path.Length == 0 ? OperationPath : $"{OperationPath}.{path}";
        if (!_errors.TryGetValue(key, out var messages)) _errors[key] = messages = [];
        messages.Add(message);
    }

    /// <summary>Refuses the operation because a precondition fails (409), such as a target another author deleted.</summary>
    /// <param name="title">The short summary.</param>
    /// <param name="detail">What failed.</param>
    public void Conflict(string title, string detail)
    {
        _conflict ??= Refusal.Conflict(title, detail);
    }

    /// <summary>The prefix of every error key of the operation being applied.</summary>
    internal string OperationPath => string.Create(CultureInfo.InvariantCulture, $"operations[{OperationIndex}]");

    /// <summary>Starts the next operation.</summary>
    /// <param name="index">Its index in the change set.</param>
    internal void Begin(int index)
    {
        OperationIndex = index;
    }

    /// <summary>The refusal the operation raised, if any: a conflict first, otherwise its input errors.</summary>
    /// <returns>The refusal, or null when the operation applied.</returns>
    internal Refusal? Raised()
    {
        if (_conflict is not null) return _conflict;
        if (_errors.Count == 0) return null;
        return Refusal.Invalid(_errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal));
    }
}
