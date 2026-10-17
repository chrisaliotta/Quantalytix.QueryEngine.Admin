namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>version_kind</c> vocabulary: the kinds of versioned object whose status the version workflow changes.</summary>
public static class VersionKinds
{
    /// <summary>Immutable definition of a dataset (<c>qe_dataset_version</c>).</summary>
    public const string DatasetVersion = "dataset_version";

    /// <summary>Effective-dated content of a canonical dimension (<c>qe_dimension_version</c>).</summary>
    public const string DimensionVersion = "dimension_version";

    /// <summary>Source-code mapping content for one dimension version (<c>qe_mapping_version</c>).</summary>
    public const string MappingVersion = "mapping_version";

    /// <summary>Pinned endpoints and assignments of a bridge (<c>qe_bridge_version</c>).</summary>
    public const string BridgeVersion = "bridge_version";

    /// <summary>Manifest of the structural semantic model (<c>qe_semantic_release</c>).</summary>
    public const string SemanticRelease = "semantic_release";

    /// <summary>Snapshot that pins exact dimension versions (<c>qe_assumption_set</c>).</summary>
    public const string AssumptionSet = "assumption_set";

    /// <summary>Variables, sources, profiles, formulas and calculation dimensions of a model (<c>qe_calculation_model_version</c>).</summary>
    public const string CalculationModelVersion = "calculation_model_version";

    /// <summary>Lines, columns and defaults of a report over one model version (<c>qe_report_version</c>).</summary>
    public const string ReportVersion = "report_version";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "version_kind",
        Description = "The kinds of versioned object whose status the version workflow changes.",
        Values =
        [
            new() { Value = DatasetVersion, Description = "Immutable definition of a dataset (qe_dataset_version)." },
            new() { Value = DimensionVersion, Description = "Effective-dated content of a canonical dimension (qe_dimension_version)." },
            new() { Value = MappingVersion, Description = "Source-code mapping content for one dimension version (qe_mapping_version)." },
            new() { Value = BridgeVersion, Description = "Pinned endpoints and assignments of a bridge (qe_bridge_version)." },
            new() { Value = SemanticRelease, Description = "Manifest of the structural semantic model (qe_semantic_release)." },
            new() { Value = AssumptionSet, Description = "Snapshot that pins exact dimension versions (qe_assumption_set)." },
            new()
            {
                Value = CalculationModelVersion,
                Description = "Variables, sources, profiles, formulas and calculation dimensions of a model (qe_calculation_model_version)."
            },
            new() { Value = ReportVersion, Description = "Lines, columns and defaults of a report over one model version (qe_report_version)." }
        ]
    };
}
