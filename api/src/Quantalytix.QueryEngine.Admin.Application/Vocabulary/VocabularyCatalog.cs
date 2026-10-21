using System.Collections.Frozen;

namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>Every controlled vocabulary, as the web client loads them once and looks values up by name.</summary>
public static class VocabularyCatalog
{
    /// <summary>Every vocabulary definition.</summary>
    public static readonly IReadOnlyList<Vocabulary> All =
    [
        PublicationStatuses.Definition,
        AssumptionSetStatuses.Definition,
        DataTypes.Definition,
        MeasureOutputDataTypes.Definition,
        Periodicities.Definition,
        SourceObjectKinds.Definition,
        FieldSourceKinds.Definition,
        GrainKinds.Definition,
        DimensionKinds.Definition,
        DefinitionOrigins.Definition,
        DimensionPurposes.Definition,
        RangeDataTypes.Definition,
        BindingKinds.Definition,
        GroupingModes.Definition,
        RaggedBehaviors.Definition,
        PeriodAlignments.Definition,
        UnmappedBehaviors.Definition,
        TimeValueTypes.Definition,
        ObservationKinds.Definition,
        CumulativeOrigins.Definition,
        MeasureKinds.Definition,
        AggregationFunctions.Definition,
        ValueNullBehaviors.Definition,
        NegativeWeightBehaviors.Definition,
        EmptySetBehaviors.Definition,
        BridgeFactorBehaviors.Definition,
        BridgeAllocationBehaviors.Definition,
        TotalBehaviors.Definition,
        RoundingModes.Definition,
        ComponentRoles.Definition,
        ObservationSelections.Definition,
        ValueTimeRoles.Definition,
        BoundaryMatches.Definition,
        DurationUnits.Definition,
        PartialIntervalBehaviors.Definition,
        MissingPeriodBehaviors.Definition,
        Cardinalities.Definition,
        AllocationModes.Definition,
        SourceSelections.Definition,
        ProviderIds.Definition,
        SelectionScopes.Definition,
        VariableMissingBehaviors.Definition,
        FormulaLanguageVersions.Definition,
        AttributionMethods.Definition,
        ReportMappingMethods.Definition,
        ReportLineKinds.Definition,
        ReportRowKinds.Definition,
        ReportValueKinds.Definition,
        SubtotalPositions.Definition,
        ReportLayouts.Definition,
        ReportTransforms.Definition,
        ComparisonPeriodTypes.Definition,
        VersionKinds.Definition,
        LookupKinds.Definition
    ];

    /// <summary>The vocabularies by name.</summary>
    private static readonly FrozenDictionary<string, Vocabulary> ByName = All.ToFrozenDictionary(vocabulary => vocabulary.Name, StringComparer.Ordinal);

    /// <summary>Finds a vocabulary by its name.</summary>
    /// <param name="name">A vocabulary name, such as <c>publication_status</c>.</param>
    /// <returns>The vocabulary, or null when no vocabulary has the name.</returns>
    public static Vocabulary? Find(string name)
    {
        return ByName.GetValueOrDefault(name);
    }
}
