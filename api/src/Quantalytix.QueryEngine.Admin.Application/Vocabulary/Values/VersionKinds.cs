using System.Collections.Frozen;

namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>version_kind</c> vocabulary: the kinds of versioned object whose status the version workflow changes. Dataset versions
/// and the releases that pin them are strict: only drafts change. Every other kind is living: each identity has at most one
/// published version, which queries always use and which can be edited in place. Calculation models and reports are not
/// versioned.
/// </summary>
public static class VersionKinds
{
    /// <summary>Immutable definition of a dataset (<c>qe_dataset_version</c>).</summary>
    public const string DatasetVersion = "dataset_version";

    /// <summary>Levels, members and range bands of a canonical dimension (<c>qe_dimension_version</c>).</summary>
    public const string DimensionVersion = "dimension_version";

    /// <summary>Source-code mapping entries of a mapping set (<c>qe_mapping_version</c>).</summary>
    public const string MappingVersion = "mapping_version";

    /// <summary>Cardinality, allocation and assignments of a bridge (<c>qe_bridge_version</c>).</summary>
    public const string BridgeVersion = "bridge_version";

    /// <summary>Manifest of the structural semantic model (<c>qe_semantic_release</c>).</summary>
    public const string SemanticRelease = "semantic_release";

    /// <summary>
    /// The living kinds: a published version can be edited, publishing a draft retires the version published before it, and
    /// queries reach the change without a release activation.
    /// </summary>
    public static readonly FrozenSet<string> Living = FrozenSet.Create(
        StringComparer.Ordinal,
        DimensionVersion,
        MappingVersion,
        BridgeVersion);

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "version_kind",
        Description = "The kinds of versioned object whose status the version workflow changes.",
        Values =
        [
            new() { Value = DatasetVersion, Description = "Immutable definition of a dataset (qe_dataset_version)." },
            new() { Value = DimensionVersion, Description = "Levels, members and range bands of a canonical dimension (qe_dimension_version)." },
            new() { Value = MappingVersion, Description = "Source-code mapping entries of a mapping set (qe_mapping_version)." },
            new() { Value = BridgeVersion, Description = "Cardinality, allocation and assignments of a bridge (qe_bridge_version)." },
            new() { Value = SemanticRelease, Description = "Manifest of the structural semantic model (qe_semantic_release)." }
        ]
    };
}
