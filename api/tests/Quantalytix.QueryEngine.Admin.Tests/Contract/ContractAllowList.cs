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
        ["Lookups"] =
        [
            "GET /api/tenants/{tenantId}/lookups/{kind}"
        ]
    };
}
