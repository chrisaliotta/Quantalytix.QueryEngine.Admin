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

    /// <summary>The frozen snapshots.</summary>
    public required int FrozenSnapshots { get; init; }

    /// <summary>The calculation models.</summary>
    public required int Models { get; init; }

    /// <summary>The reports.</summary>
    public required int Reports { get; init; }

    /// <summary>The measures of the live dataset versions: the measures queries that name no snapshot can use.</summary>
    public required int LiveMeasures { get; init; }
}
