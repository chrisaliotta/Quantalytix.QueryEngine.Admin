using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report rules as the report kind's <see cref="IVersionRules"/>, which restore runs on a stored version: every report rule, in the
/// scope of the version's snapshot, as the gate of an enabled report and the commit findings of a disabled one do.
/// </summary>
/// <param name="loader">Reads the version.</param>
/// <param name="rules">Runs the report rules.</param>
public sealed class ReportVersionRules(ReportSnapshotLoader loader, ReportRules rules) : IVersionRules
{
    /// <summary><c>report</c>.</summary>
    public string Kind => SnapshotKinds.Report;

    /// <summary>Runs every report rule on a stored version.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync([(await loader.LoadVersionAsync(versionId, cancellationToken)).Content], cancellationToken);
    }
}
