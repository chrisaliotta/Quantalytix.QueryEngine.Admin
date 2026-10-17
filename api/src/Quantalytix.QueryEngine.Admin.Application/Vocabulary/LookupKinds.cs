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

    /// <summary>Bridges.</summary>
    public const string Bridges = "bridges";

    /// <summary>Versions of a bridge; the parent is the bridge.</summary>
    public const string BridgeVersions = "bridge-versions";

    /// <summary>Semantic releases.</summary>
    public const string Releases = "releases";

    /// <summary>Assumption sets.</summary>
    public const string AssumptionSets = "assumption-sets";

    /// <summary>Calculation models.</summary>
    public const string CalculationModels = "calculation-models";

    /// <summary>Versions of a calculation model; the parent is the model.</summary>
    public const string ModelVersions = "model-versions";

    /// <summary>Variables of a model version; the parent is the model version.</summary>
    public const string Variables = "variables";

    /// <summary>Variable sources of a model version; the parent is the model version.</summary>
    public const string VariableSources = "variable-sources";

    /// <summary>Source profiles of a model version; the parent is the model version.</summary>
    public const string SourceProfiles = "source-profiles";

    /// <summary>Calculated measures of a model version; the parent is the model version.</summary>
    public const string CalculatedMeasures = "calculated-measures";

    /// <summary>Calculation dimensions of a model version; the parent is the model version.</summary>
    public const string CalculationDimensions = "calculation-dimensions";

    /// <summary>Reports.</summary>
    public const string Reports = "reports";

    /// <summary>Versions of a report; the parent is the report.</summary>
    public const string ReportVersions = "report-versions";

    /// <summary>Columns of a report version; the parent is the report version.</summary>
    public const string ReportColumns = "report-columns";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "lookup_kind",
        Description = "The picker lists the lookups endpoint serves; the value appears in the URL.",
        Values =
        [
            new() { Value = Datasets, Description = "Datasets." },
            new() { Value = DatasetVersions, Description = "Versions of a dataset; the parent is the dataset." },
            new() { Value = Fields, Description = "Fields of a dataset version; the parent is the dataset version." },
            new() { Value = Grains, Description = "Grains of a dataset version; the parent is the dataset version." },
            new() { Value = Bindings, Description = "Dimension bindings of a dataset version; the parent is the dataset version." },
            new() { Value = TimeBindings, Description = "Dimension bindings with a time extension; the parent is the dataset version." },
            new() { Value = Measures, Description = "Measures of a dataset version; the parent is the dataset version." },
            new() { Value = Dimensions, Description = "Canonical dimensions." },
            new() { Value = DimensionVersions, Description = "Versions of a dimension; the parent is the dimension." },
            new() { Value = Levels, Description = "Levels of a dimension version; the parent is the dimension version." },
            new() { Value = Members, Description = "Members of a dimension version; the parent is the dimension version." },
            new() { Value = MappingSets, Description = "Mapping sets; an optional parent narrows them to one dimension." },
            new() { Value = MappingVersions, Description = "Versions of a mapping set; the parent is the mapping set." },
            new() { Value = Bridges, Description = "Bridges." },
            new() { Value = BridgeVersions, Description = "Versions of a bridge; the parent is the bridge." },
            new() { Value = Releases, Description = "Semantic releases." },
            new() { Value = AssumptionSets, Description = "Assumption sets." },
            new() { Value = CalculationModels, Description = "Calculation models." },
            new() { Value = ModelVersions, Description = "Versions of a calculation model; the parent is the model." },
            new() { Value = Variables, Description = "Variables of a model version; the parent is the model version." },
            new() { Value = VariableSources, Description = "Variable sources of a model version; the parent is the model version." },
            new() { Value = SourceProfiles, Description = "Source profiles of a model version; the parent is the model version." },
            new() { Value = CalculatedMeasures, Description = "Calculated measures of a model version; the parent is the model version." },
            new() { Value = CalculationDimensions, Description = "Calculation dimensions of a model version; the parent is the model version." },
            new() { Value = Reports, Description = "Reports." },
            new() { Value = ReportVersions, Description = "Versions of a report; the parent is the report." },
            new() { Value = ReportColumns, Description = "Columns of a report version; the parent is the report version." }
        ]
    };
}
