using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>The table, database, primary key and JSON columns of every metadata record.</summary>
public static class TableCatalog
{
    /// <summary>The table definition of each record type.</summary>
    public static readonly FrozenDictionary<Type, TableDefinition> Tables = new Dictionary<Type, TableDefinition>
    {
        [typeof(TenantDirectoryRecord)] = ControlPlane("qe_tenant_directory", [nameof(TenantDirectoryRecord.TenantId)]),
        [typeof(DatabaseInfoRecord)] = Tenant("qe_database_info", [nameof(DatabaseInfoRecord.SingletonId)], []),
        [typeof(SemanticReleaseRecord)] = Tenant("qe_semantic_release", [nameof(SemanticReleaseRecord.SemanticReleaseId)], []),
        [typeof(ReleaseDatasetRecord)] = Tenant(
            "qe_release_dataset", [nameof(ReleaseDatasetRecord.SemanticReleaseId), nameof(ReleaseDatasetRecord.DatasetId)], []),
        [typeof(DatasetRecord)] = Tenant("qe_dataset", [nameof(DatasetRecord.DatasetId)], []),
        [typeof(DatasetVersionRecord)] = Tenant(
            "qe_dataset_version", [nameof(DatasetVersionRecord.DatasetVersionId)], [nameof(DatasetVersionRecord.Properties)]),
        [typeof(FieldRecord)] = Tenant("qe_field", [nameof(FieldRecord.FieldId)], []),
        [typeof(DatasetGrainRecord)] = Tenant("qe_dataset_grain", [nameof(DatasetGrainRecord.DatasetGrainId)], []),
        [typeof(DatasetGrainFieldRecord)] = Tenant(
            "qe_dataset_grain_field", [nameof(DatasetGrainFieldRecord.DatasetGrainId), nameof(DatasetGrainFieldRecord.Ordinal)], []),
        [typeof(DimensionRecord)] = Tenant("qe_dimension", [nameof(DimensionRecord.DimensionId)], []),
        [typeof(DimensionRoleRecord)] = Tenant("qe_dimension_role", [nameof(DimensionRoleRecord.DimensionRoleId)], []),
        [typeof(DimensionVersionRecord)] = Tenant("qe_dimension_version", [nameof(DimensionVersionRecord.DimensionVersionId)], []),
        [typeof(DimensionLevelRecord)] = Tenant("qe_dimension_level", [nameof(DimensionLevelRecord.DimensionLevelId)], []),
        [typeof(DimensionMemberRecord)] = Tenant(
            "qe_dimension_member", [nameof(DimensionMemberRecord.DimensionMemberId)], [nameof(DimensionMemberRecord.Properties)]),
        [typeof(DimensionClosureRecord)] = Tenant(
            "qe_dimension_closure",
            [
                nameof(DimensionClosureRecord.DimensionVersionId),
                nameof(DimensionClosureRecord.AncestorDimensionMemberId),
                nameof(DimensionClosureRecord.DescendantDimensionMemberId)
            ],
            []),
        [typeof(RangeBandRecord)] = Tenant(
            "qe_range_band", [nameof(RangeBandRecord.DimensionVersionId), nameof(RangeBandRecord.Ordinal)], []),
        [typeof(MappingSetRecord)] = Tenant("qe_mapping_set", [nameof(MappingSetRecord.MappingSetId)], []),
        [typeof(MappingVersionRecord)] = Tenant("qe_mapping_version", [nameof(MappingVersionRecord.MappingVersionId)], []),
        [typeof(DimensionMappingRecord)] = Tenant("qe_dimension_mapping", [nameof(DimensionMappingRecord.DimensionMappingId)], []),
        [typeof(AssumptionSetRecord)] = Tenant("qe_assumption_set", [nameof(AssumptionSetRecord.AssumptionSetId)], []),
        [typeof(AssumptionSetDimensionRecord)] = Tenant(
            "qe_assumption_set_dimension",
            [nameof(AssumptionSetDimensionRecord.AssumptionSetId), nameof(AssumptionSetDimensionRecord.DimensionId)],
            []),
        [typeof(AssumptionSetMappingRecord)] = Tenant(
            "qe_assumption_set_mapping",
            [nameof(AssumptionSetMappingRecord.AssumptionSetId), nameof(AssumptionSetMappingRecord.MappingSetId)],
            []),
        [typeof(AssumptionSetBridgeRecord)] = Tenant(
            "qe_assumption_set_bridge",
            [nameof(AssumptionSetBridgeRecord.AssumptionSetId), nameof(AssumptionSetBridgeRecord.BridgeId)],
            []),
        [typeof(BridgeRecord)] = Tenant("qe_bridge", [nameof(BridgeRecord.BridgeId)], []),
        [typeof(BridgeVersionRecord)] = Tenant("qe_bridge_version", [nameof(BridgeVersionRecord.BridgeVersionId)], []),
        [typeof(BridgeMappingRecord)] = Tenant("qe_bridge_mapping", [nameof(BridgeMappingRecord.BridgeMappingId)], []),
        [typeof(DimensionBindingRecord)] = Tenant("qe_dimension_binding", [nameof(DimensionBindingRecord.DimensionBindingId)], []),
        [typeof(TimeBindingRecord)] = Tenant(
            "qe_time_binding", [nameof(TimeBindingRecord.DimensionBindingId)], [nameof(TimeBindingRecord.AllowedPeriodicitiesJson)]),
        [typeof(MeasureRecord)] = Tenant("qe_measure", [nameof(MeasureRecord.MeasureId)], [nameof(MeasureRecord.ConfigurationJson)]),
        [typeof(MeasureComponentRecord)] = Tenant(
            "qe_measure_component", [nameof(MeasureComponentRecord.MeasureId), nameof(MeasureComponentRecord.ComponentRole)], []),
        [typeof(MeasureDependencyRecord)] = Tenant(
            "qe_measure_dependency", [nameof(MeasureDependencyRecord.MeasureId), nameof(MeasureDependencyRecord.DependencyMeasureId)], []),
        [typeof(MeasureTimePolicyRecord)] = Tenant("qe_measure_time_policy", [nameof(MeasureTimePolicyRecord.MeasureId)], []),
        [typeof(CalculationModelRecord)] = Tenant("qe_calculation_model", [nameof(CalculationModelRecord.CalculationModelId)], []),
        [typeof(VariableRecord)] = Tenant("qe_variable", [nameof(VariableRecord.VariableId)], []),
        [typeof(ReportSourceRecord)] = Tenant("qe_report_source", [nameof(ReportSourceRecord.ReportSourceId)], []),
        [typeof(ReportSourceVariableRecord)] = Tenant(
            "qe_report_source_variable",
            [nameof(ReportSourceVariableRecord.ReportSourceId), nameof(ReportSourceVariableRecord.VariableKey)],
            [nameof(ReportSourceVariableRecord.SourceFilterJson)]),
        [typeof(SourceProfileRecord)] = Tenant("qe_calculation_source_profile", [nameof(SourceProfileRecord.CalculationSourceProfileId)], []),
        [typeof(SourceProfileVariableRecord)] = Tenant(
            "qe_calculation_source_profile_variable",
            [nameof(SourceProfileVariableRecord.CalculationSourceProfileId), nameof(SourceProfileVariableRecord.VariableKey)],
            []),
        [typeof(CalculatedMeasureRecord)] = Tenant("qe_calculated_measure", [nameof(CalculatedMeasureRecord.CalculatedMeasureId)], []),
        [typeof(MetricAttributionRecord)] = Tenant(
            "qe_metric_attribution", [nameof(MetricAttributionRecord.CalculationModelId), nameof(MetricAttributionRecord.MetricKey)], []),
        [typeof(MetricAttributionDriverRecord)] = Tenant(
            "qe_metric_attribution_driver",
            [nameof(MetricAttributionDriverRecord.CalculationModelId), nameof(MetricAttributionDriverRecord.MetricKey), nameof(MetricAttributionDriverRecord.DriverKey)],
            [nameof(MetricAttributionDriverRecord.InputKeysJson)]),
        [typeof(ModelParameterRecord)] = Tenant(
            "qe_model_parameter",
            [nameof(ModelParameterRecord.CalculationModelId), nameof(ModelParameterRecord.ParameterKey)],
            [nameof(ModelParameterRecord.AllowedValuesJson)]),
        [typeof(CalculationDimensionRecord)] = Tenant(
            "qe_calculation_dimension", [nameof(CalculationDimensionRecord.CalculationDimensionId)], []),
        [typeof(ReportRecord)] = Tenant(
            "qe_report",
            [nameof(ReportRecord.ReportId)],
            [nameof(ReportRecord.PayloadJson), nameof(ReportRecord.DefaultFilterJson), nameof(ReportRecord.DefaultSettingsJson), nameof(ReportRecord.DefaultPivotJson)]),
        [typeof(ReportLineRecord)] = Tenant(
            "qe_report_line",
            [nameof(ReportLineRecord.ReportLineId)],
            [nameof(ReportLineRecord.PayloadJson), nameof(ReportLineRecord.FilterJson), nameof(ReportLineRecord.SettingsJson)]),
        [typeof(ReportColumnRecord)] = Tenant(
            "qe_report_column", [nameof(ReportColumnRecord.ReportColumnId)], [nameof(ReportColumnRecord.PayloadJson)]),
        [typeof(ReportMemberOptionRecord)] = Tenant(
            "qe_report_member_option", [nameof(ReportMemberOptionRecord.ReportId), nameof(ReportMemberOptionRecord.MemberKey)], []),
        [typeof(ReportVariableScopeRecord)] = Tenant(
            "qe_report_variable_scope", [nameof(ReportVariableScopeRecord.ReportId), nameof(ReportVariableScopeRecord.VariableKey)], []),
        [typeof(ReportScenarioRecord)] = Tenant(
            "qe_report_scenario", [nameof(ReportScenarioRecord.ReportId), nameof(ReportScenarioRecord.DimensionRoleId)], []),
        [typeof(ReportRowParameterRecord)] = Tenant(
            "qe_report_row_parameter",
            [nameof(ReportRowParameterRecord.ReportId), nameof(ReportRowParameterRecord.RowKey), nameof(ReportRowParameterRecord.ParameterKey)],
            []),
        [typeof(ReportStyleRecord)] = Tenant("qe_report_style", [nameof(ReportStyleRecord.ReportId), nameof(ReportStyleRecord.StyleKey)], [])
    }.ToFrozenDictionary();

    /// <summary>Returns the table a record type mirrors.</summary>
    /// <typeparam name="TRecord">A record from the Metadata feature.</typeparam>
    /// <returns>The record's table definition.</returns>
    public static TableDefinition For<TRecord>() where TRecord : class
    {
        return Tables[typeof(TRecord)];
    }

    /// <summary>Defines a control-plane table, none of which has JSON columns.</summary>
    private static TableDefinition ControlPlane(string name, IReadOnlyList<string> keyProperties)
    {
        return new TableDefinition { Name = name, Database = TableDatabase.ControlPlane, KeyProperties = keyProperties, JsonProperties = [] };
    }

    /// <summary>Defines a tenant table.</summary>
    private static TableDefinition Tenant(string name, IReadOnlyList<string> keyProperties, IReadOnlyList<string> jsonProperties)
    {
        return new TableDefinition { Name = name, Database = TableDatabase.Tenant, KeyProperties = keyProperties, JsonProperties = jsonProperties };
    }
}
