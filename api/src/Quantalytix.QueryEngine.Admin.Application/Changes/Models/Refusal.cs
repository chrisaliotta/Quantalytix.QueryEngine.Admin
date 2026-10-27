using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>Why a change set or a lifecycle command wrote nothing; the API answers it as the matching problem.</summary>
public sealed record Refusal
{
    /// <summary>The kind, which decides the HTTP status.</summary>
    public required RefusalKind Kind { get; init; }

    /// <summary>The short summary of a conflict, such as <c>Version is not live</c>; null otherwise.</summary>
    public string? Title { get; init; }

    /// <summary>What was refused, in a sentence; null when the errors or violations say it all.</summary>
    public string? Detail { get; init; }

    /// <summary>Input path to messages, for <see cref="RefusalKind.Invalid"/>; empty otherwise.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>The broken rules, for <see cref="RefusalKind.RulesViolated"/>; empty otherwise.</summary>
    public IReadOnlyList<RuleViolation> Violations { get; init; } = [];

    /// <summary>An input refusal.</summary>
    /// <param name="errors">Input path to messages, such as <c>operations[3].memberKey</c>.</param>
    /// <returns>The refusal.</returns>
    public static Refusal Invalid(IReadOnlyDictionary<string, string[]> errors)
    {
        return new Refusal { Kind = RefusalKind.Invalid, Errors = errors };
    }

    /// <summary>A precondition refusal.</summary>
    /// <param name="title">The short summary, such as <c>Version is not live</c>.</param>
    /// <param name="detail">What failed.</param>
    /// <returns>The refusal.</returns>
    public static Refusal Conflict(string title, string detail)
    {
        return new Refusal { Kind = RefusalKind.Conflict, Title = title, Detail = detail };
    }

    /// <summary>A rule refusal.</summary>
    /// <param name="detail">What was refused, or null.</param>
    /// <param name="violations">The rules the result would break.</param>
    /// <returns>The refusal.</returns>
    public static Refusal RulesViolated(string? detail, IReadOnlyList<RuleViolation> violations)
    {
        return new Refusal { Kind = RefusalKind.RulesViolated, Detail = detail, Violations = violations };
    }
}
