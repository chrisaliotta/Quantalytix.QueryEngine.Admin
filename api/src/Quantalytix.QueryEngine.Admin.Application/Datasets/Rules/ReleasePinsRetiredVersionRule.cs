using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>release_pins_retired_version</c>: a dataset version the active release pins cannot be retired (blueprint 11). The query engine
/// reads a dataset only through the version the active release pins (D2), so retiring it would take the dataset out of every query;
/// another release that pins a different version must be activated first. The retire command runs it through the version workflow.
/// </summary>
public sealed class ReleasePinsRetiredVersionRule(ITableGateway<DatasetVersionRecord> versions, DatasetReleaseReader releases) : IRetirementRule
{
    /// <summary>The rule applies to dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports the active release when it pins the version.</summary>
    /// <param name="versionId">The dataset version being retired.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation naming the active release; none otherwise.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        if ((await releases.ReadAsync(version.DatasetId, cancellationToken)).ActivePinning(versionId) is not { } active) return [];
        return
        [
            new RuleViolation
            {
                Code = "release_pins_retired_version",
                Message = $"The active release '{active.ReleaseKey}' pins version v{version.VersionNumber}; activate a release that pins another version before retiring it."
            }
        ];
    }
}
