namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// The contract's <c>DeleteBlockerCode</c> list (blueprint 01, Delete checks), one for every module. <c>referenced_by_&lt;kind&gt;</c>
/// names the kind of object that still reads the target; the other codes name a state of the target itself.
/// </summary>
public static class DeleteBlockerCodes
{
    /// <summary>A dimension marked not removable.</summary>
    public const string NotRemovable = "not_removable";

    /// <summary>A system dimension.</summary>
    public const string SystemDimension = "system_dimension";

    /// <summary>A dataset version that is published or retired.</summary>
    public const string PublishedVersion = "published_version";

    /// <summary>The live version of a dimension, mapping set or bridge; only frozen versions can be deleted.</summary>
    public const string LiveVersion = "live_version";

    /// <summary>An identity with published or retired versions.</summary>
    public const string HasPublishedVersions = "has_published_versions";

    /// <summary>A member with child members.</summary>
    public const string HasChildren = "has_children";

    /// <summary>A member or role the dimension's profile requires.</summary>
    public const string RequiredByProfile = "required_by_profile";

    /// <summary>A member that range bands assign.</summary>
    public const string AssignedByBands = "assigned_by_bands";

    /// <summary>A live member or level that a consumer names by key (mapping entry, bridge mapping, report rule).</summary>
    public const string NamedByConsumers = "named_by_consumers";

    /// <summary>A report-owned bridge; the report source owns it.</summary>
    public const string OwnedByReport = "owned_by_report";

    /// <summary>A level that members use.</summary>
    public const string ReferencedByMember = "referenced_by_member";

    /// <summary>A dimension, role, mapping set or bridge that a dataset binding reads.</summary>
    public const string ReferencedByDatasetBinding = "referenced_by_dataset_binding";

    /// <summary>A dimension that a mapping set targets.</summary>
    public const string ReferencedByMappingSet = "referenced_by_mapping_set";

    /// <summary>A dimension that a bridge joins.</summary>
    public const string ReferencedByBridge = "referenced_by_bridge";

    /// <summary>A dimension or role that a report calculation dimension groups by.</summary>
    public const string ReferencedByCalculationDimension = "referenced_by_calculation_dimension";

    /// <summary>A dimension or role a report uses as a scenario.</summary>
    public const string ReferencedByReportScenario = "referenced_by_report_scenario";

    /// <summary>A dimension or role a report source reads.</summary>
    public const string ReferencedByReportSource = "referenced_by_report_source";

    /// <summary>A version, mapping set or bridge that a historical assumption set pins.</summary>
    public const string ReferencedByAssumptionSet = "referenced_by_assumption_set";

    /// <summary>A dataset or dataset version that a release manifest lists.</summary>
    public const string ReferencedByRelease = "referenced_by_release";

    /// <summary>A calculation model, or a dimension as a report dimension, that a report reads.</summary>
    public const string ReferencedByReport = "referenced_by_report";
}
