namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>How much semantic metadata a tenant holds.</summary>
public sealed record OverviewCounts
{
    /// <summary>The number of datasets.</summary>
    public required int Datasets { get; init; }

    /// <summary>The number of canonical dimensions.</summary>
    public required int Dimensions { get; init; }

    /// <summary>The number of mapping sets.</summary>
    public required int MappingSets { get; init; }

    /// <summary>The number of bridges.</summary>
    public required int Bridges { get; init; }

    /// <summary>The number of semantic releases, whatever their status.</summary>
    public required int Releases { get; init; }

    /// <summary>The number of assumption sets: the active one and every historical set.</summary>
    public required int AssumptionSets { get; init; }

    /// <summary>The number of calculation models.</summary>
    public required int CalculationModels { get; init; }

    /// <summary>The number of reports.</summary>
    public required int Reports { get; init; }

    /// <summary>The number of measures of the dataset versions the active release pins: the measures queries can use.</summary>
    public required int ActiveMeasures { get; init; }
}
