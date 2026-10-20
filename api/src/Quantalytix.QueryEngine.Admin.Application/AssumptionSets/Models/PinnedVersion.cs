namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>One pin of an assumption set: the version of one identity of a living kind.</summary>
/// <param name="Kind">A living <c>version_kind</c> value.</param>
/// <param name="IdentityId">The identity, such as a dimension.</param>
/// <param name="VersionId">The pinned version.</param>
public sealed record PinnedVersion(string Kind, long IdentityId, long VersionId);
