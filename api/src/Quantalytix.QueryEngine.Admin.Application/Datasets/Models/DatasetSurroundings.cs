using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The rows and physical objects outside a dataset version aggregate that its operations, rules and read models consult, read once
/// when it loads: operations apply in memory and cannot read. A rename or a new column updates the physical catalog here too, so
/// later operations and the rules see the database as the commit leaves it.
/// </summary>
public sealed record DatasetSurroundings
{
    /// <summary>Every dataset version of the tenant, which tells who else reads a table.</summary>
    public required IReadOnlyList<DatasetVersionRecord> TenantVersions { get; init; }

    /// <summary>The release pins of the dataset.</summary>
    public required DatasetReleaseIndex Releases { get; init; }

    /// <summary>Every dimension role of the tenant.</summary>
    public required IReadOnlyList<DimensionRoleRecord> Roles { get; init; }

    /// <summary>Every dimension of the tenant.</summary>
    public required IReadOnlyList<DimensionRecord> Dimensions { get; init; }

    /// <summary>Every mapping set of the tenant.</summary>
    public required IReadOnlyList<MappingSetRecord> MappingSets { get; init; }

    /// <summary>Every bridge of the tenant.</summary>
    public required IReadOnlyList<BridgeRecord> Bridges { get; init; }

    /// <summary>The tables and views datasets can read.</summary>
    public required IReadOnlyList<SourceObject> SourceObjects { get; init; }

    /// <summary>The columns of every table and view, by object name.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>> Columns { get; init; }
}
