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
    public static readonly IReadOnlyDictionary<string, string[]> NotImplemented = new Dictionary<string, string[]>();

    /// <summary>Endpoints of the current API that the contract does not have, as <c>METHOD path</c> from admin-api.json, grouped by OpenAPI tag.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Legacy = new Dictionary<string, string[]>
    {
        ["VersionStatus"] =
        [
            "PUT /api/tenants/{tenantId}/versions/{kind}/{id}/status",
            "POST /api/tenants/{tenantId}/versions/{kind}/{id}/validation"
        ],
        ["Lookups"] =
        [
            "GET /api/tenants/{tenantId}/lookups/{kind}"
        ]
    };
}
