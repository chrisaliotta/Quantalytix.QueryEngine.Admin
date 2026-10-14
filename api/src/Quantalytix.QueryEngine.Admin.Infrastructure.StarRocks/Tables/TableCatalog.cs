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
        [typeof(SemanticReleaseRecord)] = Tenant("qe_semantic_release", [nameof(SemanticReleaseRecord.ReleaseId)], []),
        [typeof(ReleaseDatasetRecord)] = Tenant(
            "qe_release_dataset", [nameof(ReleaseDatasetRecord.ReleaseId), nameof(ReleaseDatasetRecord.DatasetId)], []),
        [typeof(DatasetRecord)] = Tenant("qe_dataset", [nameof(DatasetRecord.DatasetId)], []),
        [typeof(DatasetVersionRecord)] = Tenant(
            "qe_dataset_version", [nameof(DatasetVersionRecord.DatasetVersionId)], [nameof(DatasetVersionRecord.Properties)]),
        [typeof(FieldRecord)] = Tenant("qe_field", [nameof(FieldRecord.FieldId)], []),
        [typeof(DatasetGrainRecord)] = Tenant("qe_dataset_grain", [nameof(DatasetGrainRecord.GrainId)], []),
        [typeof(DatasetGrainFieldRecord)] = Tenant(
            "qe_dataset_grain_field", [nameof(DatasetGrainFieldRecord.GrainId), nameof(DatasetGrainFieldRecord.Ordinal)], []),
        [typeof(DimensionRecord)] = Tenant("qe_dimension", [nameof(DimensionRecord.DimensionId)], []),
        [typeof(DimensionVersionRecord)] = Tenant("qe_dimension_version", [nameof(DimensionVersionRecord.DimensionVersionId)], []),
        [typeof(DimensionLevelRecord)] = Tenant("qe_dimension_level", [nameof(DimensionLevelRecord.LevelId)], []),
        [typeof(DimensionMemberRecord)] = Tenant(
            "qe_dimension_member", [nameof(DimensionMemberRecord.MemberId)], [nameof(DimensionMemberRecord.Properties)]),
        [typeof(DimensionClosureRecord)] = Tenant(
            "qe_dimension_closure",
            [
                nameof(DimensionClosureRecord.DimensionVersionId),
                nameof(DimensionClosureRecord.AncestorMemberId),
                nameof(DimensionClosureRecord.DescendantMemberId)
            ],
            []),
        [typeof(RangeBandRecord)] = Tenant(
            "qe_range_band", [nameof(RangeBandRecord.DimensionVersionId), nameof(RangeBandRecord.Ordinal)], []),
        [typeof(MappingSetRecord)] = Tenant("qe_mapping_set", [nameof(MappingSetRecord.MappingSetId)], []),
        [typeof(MappingVersionRecord)] = Tenant("qe_mapping_version", [nameof(MappingVersionRecord.MappingVersionId)], []),
        [typeof(DimensionMappingRecord)] = Tenant("qe_dimension_mapping", [nameof(DimensionMappingRecord.MappingEntryId)], []),
        [typeof(AssumptionSetRecord)] = Tenant("qe_assumption_set", [nameof(AssumptionSetRecord.AssumptionSetId)], []),
        [typeof(AssumptionSetDimensionRecord)] = Tenant(
            "qe_assumption_set_dimension",
            [nameof(AssumptionSetDimensionRecord.AssumptionSetId), nameof(AssumptionSetDimensionRecord.DimensionId)],
            []),
        [typeof(BridgeRecord)] = Tenant("qe_bridge", [nameof(BridgeRecord.BridgeId)], []),
        [typeof(BridgeVersionRecord)] = Tenant("qe_bridge_version", [nameof(BridgeVersionRecord.BridgeVersionId)], []),
        [typeof(BridgeMappingRecord)] = Tenant(
            "qe_bridge_mapping",
            [nameof(BridgeMappingRecord.BridgeVersionId), nameof(BridgeMappingRecord.SourceMemberId), nameof(BridgeMappingRecord.TargetMemberId)],
            []),
        [typeof(DimensionBindingRecord)] = Tenant("qe_dimension_binding", [nameof(DimensionBindingRecord.BindingId)], []),
        [typeof(TimeBindingRecord)] = Tenant(
            "qe_time_binding", [nameof(TimeBindingRecord.BindingId)], [nameof(TimeBindingRecord.AllowedPeriodicitiesJson)]),
        [typeof(MeasureRecord)] = Tenant("qe_measure", [nameof(MeasureRecord.MeasureId)], [nameof(MeasureRecord.ConfigurationJson)]),
        [typeof(MeasureComponentRecord)] = Tenant(
            "qe_measure_component", [nameof(MeasureComponentRecord.MeasureId), nameof(MeasureComponentRecord.ComponentRole)], []),
        [typeof(MeasureDependencyRecord)] = Tenant(
            "qe_measure_dependency", [nameof(MeasureDependencyRecord.MeasureId), nameof(MeasureDependencyRecord.DependencyMeasureId)], []),
        [typeof(MeasureTimePolicyRecord)] = Tenant("qe_measure_time_policy", [nameof(MeasureTimePolicyRecord.MeasureId)], [])
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
