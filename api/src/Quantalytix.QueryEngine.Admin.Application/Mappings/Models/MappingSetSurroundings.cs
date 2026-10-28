using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The rows outside a mapping set aggregate that its operations, rules and read models consult, read once when it loads: a change
/// set's operations apply in memory and cannot read.
/// </summary>
public sealed record MappingSetSurroundings
{
    /// <summary>The dimension the entries map into.</summary>
    public required DimensionRecord Dimension { get; init; }

    /// <summary>The keys of the dimension's live version, which entries resolve in; null while it has none.</summary>
    public required DimensionKeys? Published { get; init; }

    /// <summary>Every version of the mapping set as stored, by version number.</summary>
    public required IReadOnlyList<MappingVersionRecord> Versions { get; init; }

    /// <summary>The entry count of every version, by version id.</summary>
    public required IReadOnlyDictionary<long, int> EntryCounts { get; init; }

    /// <summary>The dataset bindings that classify through the mapping set.</summary>
    public required IReadOnlyList<MappingBindingUsage> Bindings { get; init; }

    /// <summary>The assumption sets that pin a version.</summary>
    public required IReadOnlyList<MappingAssumptionPin> Pins { get; init; }
}
