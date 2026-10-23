namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_metric_attribution_driver</c> row: one driver of a metric's attribution, the inputs of its attribution expression that
/// switch together between the two values compared.
/// </summary>
public sealed record MetricAttributionDriverRecord
{
    /// <summary>The owning model; first part of the primary key.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>The key of the metric attributed; second part of the primary key.</summary>
    public required string MetricKey { get; init; }

    /// <summary>The driver's key, which names its expanded report column; third part of the primary key.</summary>
    public required string DriverKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The order sequential attribution switches the drivers in.</summary>
    public required int SortOrder { get; init; }

    /// <summary>JSON array (raw text) of the metric keys and <c>@periodDays</c> or <c>@yearDays</c> parameters that switch together.</summary>
    public required string InputKeysJson { get; init; }
}
