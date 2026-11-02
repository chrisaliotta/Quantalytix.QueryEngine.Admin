namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>The contract's <c>SnapshotCounts</c>: how many versions a snapshot holds, or a capture would copy, of each kind.</summary>
public sealed record SnapshotCounts
{
    /// <summary>The counts of some entries.</summary>
    /// <param name="entries">The entries of one snapshot.</param>
    /// <returns>The counts.</returns>
    public static SnapshotCounts Of(IEnumerable<SnapshotEntry> entries)
    {
        var byKind = entries.CountBy(entry => entry.Kind).ToDictionary(StringComparer.Ordinal);
        return new SnapshotCounts
        {
            Datasets = byKind.GetValueOrDefault(SnapshotKinds.Dataset),
            Dimensions = byKind.GetValueOrDefault(SnapshotKinds.Dimension),
            MappingSets = byKind.GetValueOrDefault(SnapshotKinds.MappingSet),
            Bridges = byKind.GetValueOrDefault(SnapshotKinds.Bridge),
            CalculationModels = byKind.GetValueOrDefault(SnapshotKinds.CalculationModel),
            Reports = byKind.GetValueOrDefault(SnapshotKinds.Report)
        };
    }

    /// <summary>Dataset versions.</summary>
    public required int Datasets { get; init; }

    /// <summary>Dimension versions, report dimensions included.</summary>
    public required int Dimensions { get; init; }

    /// <summary>Mapping versions.</summary>
    public required int MappingSets { get; init; }

    /// <summary>Bridge versions, report bridges included.</summary>
    public required int Bridges { get; init; }

    /// <summary>Calculation model versions.</summary>
    public required int CalculationModels { get; init; }

    /// <summary>Report versions.</summary>
    public required int Reports { get; init; }
}
