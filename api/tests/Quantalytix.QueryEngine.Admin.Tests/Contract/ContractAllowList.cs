namespace Quantalytix.QueryEngine.Admin.Tests.Contract;

/// <summary>
/// The two lists that let <see cref="ContractConformanceTests"/> pass while the refactor is under way, each shrinking to empty
/// by P6. A module agent removes its contract operations from <see cref="NotImplemented"/> as it implements them, and its
/// old endpoints from <see cref="Legacy"/> as it deletes them; the tests fail on an entry that is no longer true, so neither
/// list can go stale.
/// </summary>
internal static class ContractAllowList
{
    /// <summary>Contract operations not implemented yet, by operationId, grouped by the contract tag (module) that owns them.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> NotImplemented = new Dictionary<string, string[]>
    {
        ["tenant"] =
        [
            "getSession",
            "getVocabularies",
            "getOverview",
            "getCatalog",
            "getPublishedMembers",
            "getSystemTenant",
            "updateSystemTenant",
            "setSystemTenantEnabled"
        ],
        ["datasets"] =
        [
            "listSourceObjects",
            "getSourceColumns",
            "listDatasets",
            "createDataset",
            "getDataset",
            "deleteDataset",
            "checkDeleteDataset",
            "createDatasetDraft",
            "getDatasetVersion",
            "deleteDatasetDraft",
            "checkDeleteDatasetVersion",
            "previewDatasetChanges",
            "commitDatasetChanges",
            "checkDatasetVersion",
            "validateDatasetVersion",
            "publishDatasetVersion",
            "reopenDatasetVersion",
            "retireDatasetVersion",
            "materializeDatasetField",
            "dematerializeDatasetField",
            "refreshDatasetFieldMaterialization"
        ],
        ["mappings"] =
        [
            "listMappingSets",
            "createMappingSet",
            "getMappingSet",
            "deleteMappingSet",
            "checkDeleteMappingSet",
            "previewMappingSetChanges",
            "commitMappingSetChanges",
            "getMappingVersion",
            "deleteMappingVersion",
            "checkMappingVersion",
            "freezeMappingVersion",
            "checkDeleteMappingVersion",
            "listBridges",
            "createBridge",
            "getBridge",
            "deleteBridge",
            "checkDeleteBridge",
            "previewBridgeChanges",
            "commitBridgeChanges",
            "getBridgeVersion",
            "deleteBridgeVersion",
            "checkBridgeVersion",
            "freezeBridgeVersion",
            "checkDeleteBridgeVersion"
        ],
        ["releases"] =
        [
            "listReleases",
            "createRelease",
            "diffReleases",
            "getRelease",
            "deleteRelease",
            "previewReleaseChanges",
            "commitReleaseChanges",
            "checkRelease",
            "validateRelease",
            "publishRelease",
            "backToDraft",
            "retireRelease",
            "activateRelease",
            "listManifestDatasets",
            "listIdentities",
            "listReportReads",
            "listSets",
            "captureSet",
            "capturePreview",
            "compareSets",
            "getSet",
            "relabelSet",
            "deleteSet"
        ],
        ["models"] =
        [
            "listModels",
            "createModel",
            "getModel",
            "deleteModel",
            "checkDeleteModel",
            "checkFormula",
            "previewModelChanges",
            "commitModelChanges"
        ],
        ["reports"] =
        [
            "listReports",
            "createReport",
            "getReport",
            "deleteReport",
            "getReportDiagnostics",
            "getReportUnmappedItems",
            "previewReportChanges",
            "commitReportChanges",
            "setReportEnabled"
        ]
    };

    /// <summary>Endpoints of the current API that the contract does not have, as <c>METHOD path</c> from admin-api.json, grouped by OpenAPI tag.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Legacy = new Dictionary<string, string[]>
    {
        ["VersionStatus"] =
        [
            "PUT /api/tenants/{tenantId}/versions/{kind}/{id}/status",
            "POST /api/tenants/{tenantId}/versions/{kind}/{id}/validation"
        ],
        ["SourceObjects"] =
        [
            "GET /api/tenants/{tenantId}/source-objects/{name}/columns"
        ],
        ["CalculationDimensions"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/calculation-dimensions",
            "POST /api/tenants/{tenantId}/reports/{reportId}/calculation-dimensions",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/calculation-dimensions/{itemId}",
            "DELETE /api/tenants/{tenantId}/reports/{reportId}/calculation-dimensions/{itemId}"
        ],
        ["ReportColumns"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/columns",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/columns"
        ],
        ["ReportEditor"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/editor",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/rows"
        ],
        ["ReportScenarios"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/scenarios",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/scenarios"
        ],
        ["Reports"] =
        [
            "PUT /api/tenants/{tenantId}/reports/{reportId}",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/settings",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/status",
            "POST /api/tenants/{tenantId}/reports/{reportId}/validation"
        ],
        ["ReportSources"] =
        [
            "POST /api/tenants/{tenantId}/reports/{reportId}/sources",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}",
            "DELETE /api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}/variables",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/sources/{sourceId}/items"
        ],
        ["ReportStyles"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/styles",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/styles/{styleKey}",
            "DELETE /api/tenants/{tenantId}/reports/{reportId}/styles/{styleKey}"
        ],
        ["ReportVariableScopes"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/variable-scopes",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/variable-scopes"
        ],
        ["SourceProfiles"] =
        [
            "GET /api/tenants/{tenantId}/reports/{reportId}/profiles",
            "POST /api/tenants/{tenantId}/reports/{reportId}/profiles",
            "PUT /api/tenants/{tenantId}/reports/{reportId}/profiles/{itemId}",
            "DELETE /api/tenants/{tenantId}/reports/{reportId}/profiles/{itemId}"
        ],
        ["Releases"] =
        [
            "GET /api/tenants/{tenantId}/releases/{semanticReleaseId}/datasets",
            "PUT /api/tenants/{tenantId}/releases/{semanticReleaseId}/datasets"
        ],
        ["Measures"] =
        [
            "GET /api/tenants/{tenantId}/dataset-versions/{versionId}/measures",
            "POST /api/tenants/{tenantId}/dataset-versions/{versionId}/measures",
            "PUT /api/tenants/{tenantId}/dataset-versions/{versionId}/measures/{measureId}",
            "DELETE /api/tenants/{tenantId}/dataset-versions/{versionId}/measures/{measureId}"
        ],
        ["MappingEntries"] =
        [
            "GET /api/tenants/{tenantId}/mapping-versions/{versionId}/entries",
            "PUT /api/tenants/{tenantId}/mapping-versions/{versionId}/entries",
            "POST /api/tenants/{tenantId}/mapping-versions/{versionId}/entries",
            "PUT /api/tenants/{tenantId}/mapping-versions/{versionId}/entries/{entryId}",
            "DELETE /api/tenants/{tenantId}/mapping-versions/{versionId}/entries/{entryId}"
        ],
        ["MappingSets"] =
        [
            "PUT /api/tenants/{tenantId}/mapping-sets/{mappingSetId}"
        ],
        ["MappingVersions"] =
        [
            "GET /api/tenants/{tenantId}/mapping-sets/{mappingSetId}/versions",
            "POST /api/tenants/{tenantId}/mapping-sets/{mappingSetId}/versions"
        ],
        ["Lookups"] =
        [
            "GET /api/tenants/{tenantId}/lookups/{kind}"
        ],
        ["Datasets"] =
        [
            "PUT /api/tenants/{tenantId}/datasets/{datasetId}",
            "GET /api/tenants/{tenantId}/datasets/{datasetId}/versions"
        ],
        ["DatasetVersions"] =
        [
            "PUT /api/tenants/{tenantId}/dataset-versions/{versionId}"
        ],
        ["Fields"] =
        [
            "GET /api/tenants/{tenantId}/dataset-versions/{versionId}/fields",
            "POST /api/tenants/{tenantId}/dataset-versions/{versionId}/fields",
            "PUT /api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}",
            "DELETE /api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}",
            "POST /api/tenants/{tenantId}/dataset-versions/{versionId}/fields/from-columns",
            "POST /api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}/materialize",
            "PUT /api/tenants/{tenantId}/dataset-versions/{versionId}/fields/{fieldId}/materialized-column"
        ],
        ["Grains"] =
        [
            "GET /api/tenants/{tenantId}/dataset-versions/{versionId}/grains",
            "POST /api/tenants/{tenantId}/dataset-versions/{versionId}/grains",
            "PUT /api/tenants/{tenantId}/dataset-versions/{versionId}/grains/{datasetGrainId}",
            "DELETE /api/tenants/{tenantId}/dataset-versions/{versionId}/grains/{datasetGrainId}"
        ],
        ["CalculatedMeasures"] =
        [
            "GET /api/tenants/{tenantId}/calculation-models/{calculationModelId}/calculated-measures",
            "POST /api/tenants/{tenantId}/calculation-models/{calculationModelId}/calculated-measures",
            "PUT /api/tenants/{tenantId}/calculation-models/{calculationModelId}/calculated-measures/{itemId}",
            "DELETE /api/tenants/{tenantId}/calculation-models/{calculationModelId}/calculated-measures/{itemId}"
        ],
        ["CalculationModels"] =
        [
            "PUT /api/tenants/{tenantId}/calculation-models/{calculationModelId}"
        ],
        ["MetricAttributions"] =
        [
            "GET /api/tenants/{tenantId}/calculation-models/{calculationModelId}/attributions",
            "PUT /api/tenants/{tenantId}/calculation-models/{calculationModelId}/attributions/{metricKey}",
            "DELETE /api/tenants/{tenantId}/calculation-models/{calculationModelId}/attributions/{metricKey}"
        ],
        ["ModelParameters"] =
        [
            "GET /api/tenants/{tenantId}/calculation-models/{calculationModelId}/parameters",
            "PUT /api/tenants/{tenantId}/calculation-models/{calculationModelId}/parameters/{parameterKey}",
            "DELETE /api/tenants/{tenantId}/calculation-models/{calculationModelId}/parameters/{parameterKey}"
        ],
        ["Variables"] =
        [
            "GET /api/tenants/{tenantId}/calculation-models/{calculationModelId}/variables",
            "POST /api/tenants/{tenantId}/calculation-models/{calculationModelId}/variables",
            "PUT /api/tenants/{tenantId}/calculation-models/{calculationModelId}/variables/{itemId}",
            "DELETE /api/tenants/{tenantId}/calculation-models/{calculationModelId}/variables/{itemId}"
        ],
        ["BridgeMappings"] =
        [
            "GET /api/tenants/{tenantId}/bridge-versions/{versionId}/mappings",
            "PUT /api/tenants/{tenantId}/bridge-versions/{versionId}/mappings"
        ],
        ["Bridges"] =
        [
            "PUT /api/tenants/{tenantId}/bridges/{bridgeId}"
        ],
        ["BridgeVersions"] =
        [
            "GET /api/tenants/{tenantId}/bridges/{bridgeId}/versions",
            "POST /api/tenants/{tenantId}/bridges/{bridgeId}/versions",
            "PUT /api/tenants/{tenantId}/bridge-versions/{versionId}"
        ],
        ["Bindings"] =
        [
            "GET /api/tenants/{tenantId}/dataset-versions/{versionId}/bindings",
            "POST /api/tenants/{tenantId}/dataset-versions/{versionId}/bindings",
            "PUT /api/tenants/{tenantId}/dataset-versions/{versionId}/bindings/{dimensionBindingId}",
            "DELETE /api/tenants/{tenantId}/dataset-versions/{versionId}/bindings/{dimensionBindingId}"
        ]
    };
}
