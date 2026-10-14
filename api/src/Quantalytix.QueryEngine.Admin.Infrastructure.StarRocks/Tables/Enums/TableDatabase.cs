namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>The database a metadata table lives in.</summary>
public enum TableDatabase
{
    /// <summary>The shared <c>query_control</c> database.</summary>
    ControlPlane,

    /// <summary>The request tenant's own database.</summary>
    Tenant
}
