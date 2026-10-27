namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>lookup_kind</c> vocabulary: the picker lists the lookups endpoint serves. Values are kebab-case because they
/// appear in the URL.
/// </summary>
public static class LookupKinds
{
    /// <summary>Datasets.</summary>
    public const string Datasets = "datasets";

    /// <summary>Versions of a dataset; the parent is the dataset.</summary>
    public const string DatasetVersions = "dataset-versions";

    /// <summary>Fields of a dataset version; the parent is the dataset version.</summary>
    public const string Fields = "fields";

    /// <summary>Roles a dataset version binds into members, which a bridge can start from; the parent is the dataset version.</summary>
    public const string BoundRoles = "bound-roles";

    /// <summary>Grains of a dataset version; the parent is the dataset version.</summary>
    public const string Grains = "grains";

    /// <summary>Dimension bindings of a dataset version; the parent is the dataset version.</summary>
    public const string Bindings = "bindings";

    /// <summary>Dimension bindings with a time extension; the parent is the dataset version.</summary>
    public const string TimeBindings = "time-bindings";

    /// <summary>Measures of a dataset version; the parent is the dataset version.</summary>
    public const string Measures = "measures";

    /// <summary>Canonical dimensions.</summary>
    public const string Dimensions = "dimensions";

    /// <summary>Roles of dimensions, each pointing at its template; an optional parent narrows them to one dimension.</summary>
    public const string DimensionRoles = "dimension-roles";

    /// <summary>Versions of a dimension; the parent is the dimension.</summary>
    public const string DimensionVersions = "dimension-versions";

    /// <summary>Levels of a dimension version; the parent is the dimension version.</summary>
    public const string Levels = "levels";

    /// <summary>Members of a dimension version; the parent is the dimension version.</summary>
    public const string Members = "members";

    /// <summary>Mapping sets; an optional parent narrows them to one dimension.</summary>
    public const string MappingSets = "mapping-sets";

    /// <summary>Versions of a mapping set; the parent is the mapping set.</summary>
    public const string MappingVersions = "mapping-versions";

    /// <summary>Bridges, each pointing at its target dimension; an optional parent narrows them to one source dimension.</summary>
    public const string Bridges = "bridges";

    /// <summary>Versions of a bridge; the parent is the bridge.</summary>
    public const string BridgeVersions = "bridge-versions";

    /// <summary>Semantic releases.</summary>
    public const string Releases = "releases";

    /// <summary>Assumption sets, the active one first.</summary>
    public const string AssumptionSets = "assumption-sets";

    /// <summary>Calculation models, classified by their currency code.</summary>
    public const string CalculationModels = "calculation-models";

    /// <summary>Variables of a calculation model; the parent is the model.</summary>
    public const string Variables = "variables";

    /// <summary>Sources of a report, classified by their mapping method; the parent is the report.</summary>
    public const string ReportSources = "report-sources";

    /// <summary>Source profiles of a report; the parent is the report.</summary>
    public const string SourceProfiles = "source-profiles";

    /// <summary>Calculated measures of a calculation model; the parent is the model.</summary>
    public const string CalculatedMeasures = "calculated-measures";

    /// <summary>Calculation dimensions of a report; the parent is the report.</summary>
    public const string CalculationDimensions = "calculation-dimensions";

    /// <summary>Reports.</summary>
    public const string Reports = "reports";

    /// <summary>Columns of a report; the parent is the report.</summary>
    public const string ReportColumns = "report-columns";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "lookup_kind",
        Description = "The picker lists the lookups endpoint serves; the value appears in the URL.",
        Values =
        [
            new() { Value = Datasets, Label = "Datasets", Description = "Datasets." },
            new() { Value = DatasetVersions, Label = "Dataset-versions", Description = "Versions of a dataset; the parent is the dataset." },
            new() { Value = Fields, Label = "Fields", Description = "Fields of a dataset version; the parent is the dataset version." },
            new() { Value = BoundRoles, Label = "Bound-roles", Description = "Roles a dataset version binds into members with an enabled binding, each pointing at its template dimension; the parent is the dataset version." },
            new() { Value = Grains, Label = "Grains", Description = "Grains of a dataset version; the parent is the dataset version." },
            new() { Value = Bindings, Label = "Bindings", Description = "Dimension bindings of a dataset version; the parent is the dataset version." },
            new() { Value = TimeBindings, Label = "Time-bindings", Description = "Dimension bindings with a time extension; the parent is the dataset version." },
            new() { Value = Measures, Label = "Measures", Description = "Measures of a dataset version; the parent is the dataset version." },
            new() { Value = Dimensions, Label = "Dimensions", Description = "Canonical dimensions." },
            new() { Value = DimensionRoles, Label = "Dimension-roles", Description = "Roles of dimensions, labeled with their template and classified by its kind, each pointing at the template; an optional parent narrows them to one dimension." },
            new() { Value = DimensionVersions, Label = "Dimension-versions", Description = "Versions of a dimension; the parent is the dimension." },
            new() { Value = Levels, Label = "Levels", Description = "Levels of a dimension version; the parent is the dimension version." },
            new() { Value = Members, Label = "Members", Description = "Members of a dimension version; the parent is the dimension version." },
            new() { Value = MappingSets, Label = "Mapping-sets", Description = "Mapping sets; an optional parent narrows them to one dimension." },
            new() { Value = MappingVersions, Label = "Mapping-versions", Description = "Versions of a mapping set; the parent is the mapping set." },
            new() { Value = Bridges, Label = "Bridges", Description = "Bridges, each pointing at its target dimension; an optional parent narrows them to one source dimension." },
            new() { Value = BridgeVersions, Label = "Bridge-versions", Description = "Versions of a bridge; the parent is the bridge." },
            new() { Value = Releases, Label = "Releases", Description = "Semantic releases." },
            new() { Value = AssumptionSets, Label = "Assumption-sets", Description = "Assumption sets, the active one first." },
            new() { Value = CalculationModels, Label = "Calculation-models", Description = "Calculation models, classified by their currency code." },
            new() { Value = Variables, Label = "Variables", Description = "Variables of a calculation model; the parent is the model." },
            new() { Value = ReportSources, Label = "Report-sources", Description = "Sources of a report, classified by their mapping method; the parent is the report." },
            new() { Value = SourceProfiles, Label = "Source-profiles", Description = "Source profiles of a report; the parent is the report." },
            new() { Value = CalculatedMeasures, Label = "Calculated-measures", Description = "Calculated measures of a calculation model; the parent is the model." },
            new() { Value = CalculationDimensions, Label = "Calculation-dimensions", Description = "Calculation dimensions of a report; the parent is the report." },
            new() { Value = Reports, Label = "Reports", Description = "Reports." },
            new() { Value = ReportColumns, Label = "Report-columns", Description = "Columns of a report; the parent is the report." }
        ]
    };
}
