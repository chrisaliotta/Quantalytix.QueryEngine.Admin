namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>What the route value a <see cref="RequireAnalyticalAttribute"/> reads names.</summary>
public enum AnalyticalTarget
{
    /// <summary>A dimension.</summary>
    Dimension,

    /// <summary>A dimension version.</summary>
    DimensionVersion,

    /// <summary>A bridge.</summary>
    Bridge,

    /// <summary>A bridge version.</summary>
    BridgeVersion,

    /// <summary>A version of the kind the route's <c>kind</c> value names: dimension and bridge versions are checked, other kinds pass.</summary>
    RoutedVersion
}
