namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The contract's <c>DimensionUsageKind</c>: what references a dimension. Each is also the <c>&lt;kind&gt;</c> of the delete blocker
/// <c>referenced_by_&lt;kind&gt;</c>.
/// </summary>
public static class DimensionUsageKinds
{
    /// <summary>A binding of a dataset version.</summary>
    public const string DatasetBinding = "dataset_binding";

    /// <summary>A mapping set that targets the dimension.</summary>
    public const string MappingSet = "mapping_set";

    /// <summary>A bridge from or to the dimension.</summary>
    public const string Bridge = "bridge";

    /// <summary>A report calculation dimension that groups by a role.</summary>
    public const string CalculationDimension = "calculation_dimension";

    /// <summary>A report that names a role as a scenario.</summary>
    public const string ReportScenario = "report_scenario";

    /// <summary>A report source that reads a role.</summary>
    public const string ReportSource = "report_source";

    /// <summary>An assumption set that pins a version.</summary>
    public const string AssumptionSet = "assumption_set";
}
