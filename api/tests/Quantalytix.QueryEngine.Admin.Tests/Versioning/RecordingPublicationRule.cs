using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>A publication rule that writes each evaluation to a shared log and reports fixed violations.</summary>
/// <param name="kind">The <c>version_kind</c> the rule checks.</param>
/// <param name="log">The log each evaluation appends <c>{kind} rule {versionId}</c> to.</param>
/// <param name="violations">The violations every evaluation reports; none by default.</param>
internal sealed class RecordingPublicationRule(string kind, List<string> log, params IReadOnlyList<RuleViolation> violations) : IPublicationRule
{
    /// <summary>The <c>version_kind</c> the rule checks.</summary>
    public string Kind { get; } = kind;

    /// <summary>Logs the evaluation and reports the fixed violations.</summary>
    /// <param name="versionId">The version being checked.</param>
    /// <param name="cancellationToken">Unused; the rule answers synchronously.</param>
    /// <returns>The fixed violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        log.Add($"{Kind} rule {versionId}");
        return Task.FromResult(violations);
    }
}
