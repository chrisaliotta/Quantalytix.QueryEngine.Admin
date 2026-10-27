using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>version_supports_a_mode</c>: the version supports aggregate queries, detail queries or both.</summary>
public sealed class VersionSupportsAModeRule : DatasetRule
{
    /// <summary><c>version_supports_a_mode</c>.</summary>
    protected override string Code => "version_supports_a_mode";

    /// <summary>Reports a version that supports neither mode.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>At most one violation.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        if (snapshot.Version.SupportsAggregate || snapshot.Version.SupportsDetail) yield break;
        yield return Violation(DatasetTabs.Settings, $"Version v{snapshot.Version.VersionNumber} supports neither aggregate nor detail queries; enable at least one.");
    }
}
