using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The refusals the release commands share.</summary>
public static class ReleaseRefusals
{
    /// <summary>A command the release's status does not allow (409).</summary>
    /// <param name="release">The release.</param>
    /// <param name="allowed">What the command needs, such as <c>a draft</c>.</param>
    /// <returns>The refusal.</returns>
    public static Refusal WrongStatus(SemanticReleaseRecord release, string allowed)
    {
        return Refusal.Conflict($"Release is not {allowed}", $"Release '{release.ReleaseKey}' is {release.Status}; this needs {allowed}.");
    }
}
