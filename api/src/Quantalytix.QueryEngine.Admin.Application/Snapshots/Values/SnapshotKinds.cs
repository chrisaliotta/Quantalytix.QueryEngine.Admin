namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// The contract's <c>SnapshotKind</c>: the six versioned identities a snapshot holds one version of, named by identity, in the order
/// captures copy them and the snapshot reads list them.
/// </summary>
public static class SnapshotKinds
{
    /// <summary>A dataset (<c>qe_dataset_version</c>).</summary>
    public const string Dataset = "dataset";

    /// <summary>A dimension, analytical or report-owned (<c>qe_dimension_version</c>).</summary>
    public const string Dimension = "dimension";

    /// <summary>A mapping set (<c>qe_mapping_version</c>).</summary>
    public const string MappingSet = "mapping_set";

    /// <summary>A bridge, analytical or report-owned (<c>qe_bridge_version</c>).</summary>
    public const string Bridge = "bridge";

    /// <summary>A calculation model (<c>qe_calculation_model_version</c>).</summary>
    public const string CalculationModel = "calculation_model";

    /// <summary>A report (<c>qe_report_version</c>).</summary>
    public const string Report = "report";

    /// <summary>Every kind, in list order.</summary>
    public static readonly IReadOnlyList<string> All = [Dataset, Dimension, MappingSet, Bridge, CalculationModel, Report];
}
