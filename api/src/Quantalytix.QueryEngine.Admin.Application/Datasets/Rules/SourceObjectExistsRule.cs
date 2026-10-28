using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>source_object_exists</c>: the version reads a table or view of its kind that the tenant database has.</summary>
public sealed class SourceObjectExistsRule : DatasetRule
{
    /// <summary><c>source_object_exists</c>.</summary>
    protected override string Code => "source_object_exists";

    /// <summary>Reports a source object the database lacks.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>At most one violation.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var version = snapshot.Version;
        if (snapshot.Surroundings.SourceObjects.Any(item => item.Kind == version.SourceObjectKind && item.Name == version.SourceObject)) yield break;
        yield return Violation(DatasetTabs.Settings, $"The {version.SourceObjectKind} '{version.SourceObject}' does not exist in the tenant database.");
    }
}
