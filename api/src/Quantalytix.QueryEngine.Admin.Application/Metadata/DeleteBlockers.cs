using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// Collects what blocks a delete, each reason coded, and answers with the <see cref="DeleteCheck"/> or its one-sentence
/// <see cref="Detail"/>. A resource delete codes its reasons with <see cref="DeleteBlockerCodes"/>; a delete inside an
/// aggregate may use its own snake_case rule code.
/// </summary>
/// <param name="subject">What is being deleted, such as <c>Member 'CA'</c>.</param>
public sealed class DeleteBlockers(string subject)
{
    /// <summary>The most names one reason lists before it counts the rest.</summary>
    private const int ListedNames = 10;

    /// <summary>The reasons found so far.</summary>
    private readonly List<RuleViolation> _reasons = [];

    /// <summary>Adds a reason.</summary>
    /// <param name="code">The reason's code, such as <see cref="DeleteBlockerCodes.NotRemovable"/>.</param>
    /// <param name="reason">A phrase completing "cannot be deleted:", such as <c>it is not removable</c>.</param>
    public void Add(string code, string reason)
    {
        _reasons.Add(new RuleViolation { Code = code, Message = reason });
    }

    /// <summary>Adds a reason naming the blocking rows, when there are any; repeated names are listed once.</summary>
    /// <param name="code">The reason's code, such as <see cref="DeleteBlockerCodes.ReferencedByMappingSet"/>.</param>
    /// <param name="label">The phrase before the names, such as <c>referenced by mapping sets</c>.</param>
    /// <param name="names">The blocking rows, already worded, such as <c>'Origination branch'</c>.</param>
    public void AddNamed(string code, string label, IEnumerable<string> names)
    {
        var distinct = names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return;
        var listed = string.Join(", ", distinct.Take(ListedNames));
        if (distinct.Count > ListedNames) listed += $" and {distinct.Count - ListedNames} more";
        Add(code, $"{label} {listed}");
    }

    /// <summary>The delete check: allowed when no reason was added, otherwise the reasons and the sentence naming them.</summary>
    /// <returns>The check.</returns>
    public DeleteCheck Check()
    {
        return new DeleteCheck { Allowed = _reasons.Count == 0, Message = Detail(), Reasons = [.. _reasons] };
    }

    /// <summary>The 409 detail, or null when nothing blocks the delete.</summary>
    /// <returns>A sentence naming every reason, or null.</returns>
    public string? Detail()
    {
        if (_reasons.Count == 0) return null;
        return $"{subject} cannot be deleted: {string.Join("; ", _reasons.Select(reason => reason.Message))}.";
    }
}
