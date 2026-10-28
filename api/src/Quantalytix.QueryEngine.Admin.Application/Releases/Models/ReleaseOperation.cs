using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The contract's <c>ReleaseOperation</c> union, discriminated by <c>op</c> (OI-01). The release editor keeps the whole manifest as
/// its pending state, so one replace operation is the only edit.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ReleaseSaveManifestOperation), "saveManifest")]
public abstract record ReleaseOperation;

/// <summary><c>saveManifest</c>: replaces the whole manifest of a draft release.</summary>
public sealed record ReleaseSaveManifestOperation : ReleaseOperation
{
    /// <summary>The pinned dataset versions, one per dataset.</summary>
    public required IReadOnlyList<ManifestEntry> Manifest { get; init; }
}
