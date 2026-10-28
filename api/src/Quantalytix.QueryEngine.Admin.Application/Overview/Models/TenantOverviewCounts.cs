namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>The contract's <c>TenantOverviewCounts</c>: how much metadata the tenant holds.</summary>
public sealed record TenantOverviewCounts
{
    /// <summary>The datasets.</summary>
    public required int Datasets { get; init; }

    /// <summary>The analytical dimensions; report-owned dimensions are not counted (OI-22).</summary>
    public required int Dimensions { get; init; }

    /// <summary>The mapping sets.</summary>
    public required int MappingSets { get; init; }

    /// <summary>The analytical bridges; report-owned bridges are not counted (OI-22).</summary>
    public required int Bridges { get; init; }

    /// <summary>The releases, whatever their status.</summary>
    public required int Releases { get; init; }

    /// <summary>The assumption sets: the active one and every historical set.</summary>
    public required int AssumptionSets { get; init; }

    /// <summary>The calculation models.</summary>
    public required int Models { get; init; }

    /// <summary>The reports.</summary>
    public required int Reports { get; init; }

    /// <summary>The measures of the dataset versions the active release pins: the measures queries can use.</summary>
    public required int ActiveMeasures { get; init; }
}
