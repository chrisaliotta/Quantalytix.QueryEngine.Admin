namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetMaterializationState</c>: how far the materialized view holding a field's column has refreshed.</summary>
public static class MaterializationStates
{
    /// <summary>The view is being created or refreshed.</summary>
    public const string Building = "building";

    /// <summary>The view's last refresh succeeded; the engine reads the column.</summary>
    public const string Ready = "ready";

    /// <summary>The view's last refresh failed, or the view is inactive or missing.</summary>
    public const string Failed = "failed";
}
