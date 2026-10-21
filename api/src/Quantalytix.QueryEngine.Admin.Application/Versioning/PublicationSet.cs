using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The version that counts as its identity's published version while the rules of one transition run. A rule checks the keys a
/// version names against the published versions of the objects it names; while a version's own rules run, it counts as its
/// identity's published version, so the rules see the content it is about to publish rather than the version it replaces.
/// Empty outside a transition, when the published versions count.
/// </summary>
public sealed class PublicationSet
{
    /// <summary>The version that counts as published, by version kind and identity.</summary>
    private readonly Dictionary<(string Kind, long IdentityId), long> _versions = [];

    /// <summary>Counts a version as its identity's published version until <see cref="Clear"/>.</summary>
    /// <param name="kind">The living <c>version_kind</c> value of the version.</param>
    /// <param name="identityId">The identity the version belongs to, such as its dimension.</param>
    /// <param name="versionId">The version.</param>
    public void Add(string kind, long identityId, long versionId)
    {
        _versions[(kind, identityId)] = versionId;
    }

    /// <summary>Counts the published versions again.</summary>
    public void Clear()
    {
        _versions.Clear();
    }

    /// <summary>
    /// Keeps, among versions of some identities of one kind, the version of each identity that counts as published: the set's
    /// version of an identity the set holds, and the published version of every other identity.
    /// </summary>
    /// <typeparam name="TVersion">The version record.</typeparam>
    /// <param name="kind">The living <c>version_kind</c> value of the versions.</param>
    /// <param name="versions">Versions of the identities, of any status.</param>
    /// <param name="identityOf">The identity a version belongs to.</param>
    /// <param name="idOf">A version's surrogate key.</param>
    /// <param name="statusOf">A version's status.</param>
    /// <returns>At most one version per identity.</returns>
    public IReadOnlyList<TVersion> CountingAsPublished<TVersion>(
        string kind,
        IEnumerable<TVersion> versions,
        Func<TVersion, long> identityOf,
        Func<TVersion, long> idOf,
        Func<TVersion, string> statusOf)
    {
        return
        [
            .. versions.Where(version => _versions.TryGetValue((kind, identityOf(version)), out var counted)
                ? idOf(version) == counted
                : statusOf(version) == PublicationStatuses.Published)
        ];
    }
}
