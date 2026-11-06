namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>One version an <see cref="IVersionCopier"/> copies.</summary>
/// <param name="VersionId">The version to copy.</param>
/// <param name="TargetVersionId">
/// A version of the same identity whose content the copy replaces in place, keeping its id; null to insert a new version.
/// </param>
public sealed record VersionCopy(long VersionId, long? TargetVersionId);
