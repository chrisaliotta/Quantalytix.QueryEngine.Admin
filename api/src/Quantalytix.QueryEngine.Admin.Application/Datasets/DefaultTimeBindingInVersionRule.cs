using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Requires a dataset version's default time binding, when it has one, to be a binding of the same version with a time
/// extension, because queries fall back to it as their time axis.
/// </summary>
public sealed class DefaultTimeBindingInVersionRule(
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "default_time_binding_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports a default time binding that is not a time binding of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the default time binding is outside the version or has no time extension; otherwise none.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        if (version.DefaultTimeBindingId is not { } dimensionBindingId) return [];
        var inVersion = await bindings.ListAsync(new { DimensionBindingId = dimensionBindingId, DatasetVersionId = versionId }, cancellationToken);
        var extensions = await timeBindings.ListAsync(new { DimensionBindingId = dimensionBindingId }, cancellationToken);
        if (inVersion.Count == 1 && extensions.Count == 1) return [];
        return [new RuleViolation { Rule = Code, Message = $"Default time binding {dimensionBindingId} is not a time binding of this version." }];
    }
}
