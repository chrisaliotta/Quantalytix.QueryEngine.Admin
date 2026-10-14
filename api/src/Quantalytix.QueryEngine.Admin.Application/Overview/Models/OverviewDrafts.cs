namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>The number of rows in <c>draft</c> status per versioned kind: the work in progress.</summary>
public sealed record OverviewDrafts
{
    /// <summary>Draft dataset versions.</summary>
    public required int DatasetVersions { get; init; }

    /// <summary>Draft dimension versions.</summary>
    public required int DimensionVersions { get; init; }

    /// <summary>Draft mapping versions.</summary>
    public required int MappingVersions { get; init; }

    /// <summary>Draft bridge versions.</summary>
    public required int BridgeVersions { get; init; }

    /// <summary>Draft semantic releases.</summary>
    public required int Releases { get; init; }

    /// <summary>Draft assumption sets.</summary>
    public required int AssumptionSets { get; init; }
}
