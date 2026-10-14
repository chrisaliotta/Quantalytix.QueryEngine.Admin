using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>A lifecycle handler that writes each call to a shared log.</summary>
/// <param name="kind">The <c>version_kind</c> the handler serves.</param>
/// <param name="log">The log each call appends <c>{kind} validating {versionId}</c> or <c>{kind} published {versionId}</c> to.</param>
internal sealed class RecordingLifecycleHandler(string kind, List<string> log) : IVersionLifecycleHandler
{
    /// <summary>The <c>version_kind</c> the handler serves.</summary>
    public string Kind { get; } = kind;

    /// <summary>Logs that a version is being validated.</summary>
    /// <param name="versionId">The version leaving draft.</param>
    /// <param name="cancellationToken">Unused; the handler answers synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        log.Add($"{Kind} validating {versionId}");
        return Task.CompletedTask;
    }

    /// <summary>Logs that a version was published.</summary>
    /// <param name="versionId">The published version.</param>
    /// <param name="cancellationToken">Unused; the handler answers synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        log.Add($"{Kind} published {versionId}");
        return Task.CompletedTask;
    }
}
