namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>Collects what blocks a delete and words the detail of the 409 answer, naming the blocking rows.</summary>
/// <param name="subject">What is being deleted, such as <c>Member 'CA'</c>.</param>
internal sealed class DeleteBlockers(string subject)
{
    /// <summary>The most names one reason lists before it counts the rest.</summary>
    private const int ListedNames = 10;

    /// <summary>The reasons found so far.</summary>
    private readonly List<string> _reasons = [];

    /// <summary>Adds a reason.</summary>
    /// <param name="reason">A phrase completing "cannot be deleted:", such as <c>it is not removable</c>.</param>
    public void Add(string reason)
    {
        _reasons.Add(reason);
    }

    /// <summary>Adds a reason naming the blocking rows, when there are any; repeated names are listed once.</summary>
    /// <param name="label">The phrase before the names, such as <c>referenced by bindings</c>.</param>
    /// <param name="names">The blocking rows, already worded, such as <c>'Origination branch'</c>.</param>
    public void AddNamed(string label, IEnumerable<string> names)
    {
        var distinct = names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return;
        var listed = string.Join(", ", distinct.Take(ListedNames));
        if (distinct.Count > ListedNames) listed += $" and {distinct.Count - ListedNames} more";
        _reasons.Add($"{label} {listed}");
    }

    /// <summary>The 409 detail, or null when nothing blocks the delete.</summary>
    /// <returns>A sentence naming every reason, or null.</returns>
    public string? Detail()
    {
        if (_reasons.Count == 0) return null;
        return $"{subject} cannot be deleted: {string.Join("; ", _reasons)}.";
    }
}
