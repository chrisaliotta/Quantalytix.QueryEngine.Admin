namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ManifestDataset</c>: a dataset as the manifest editor shows it, with every version.</summary>
public sealed record ManifestDataset
{
    /// <summary>The dataset.</summary>
    public required long Id { get; init; }

    /// <summary>The dataset's dotted key.</summary>
    public required string Key { get; init; }

    /// <summary>The dataset's display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the dataset is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Every version, newest first.</summary>
    public required IReadOnlyList<ManifestDatasetVersion> Versions { get; init; }
}

/// <summary>The contract's <c>ManifestDatasetVersion</c>: a dataset version with what it binds and measures.</summary>
public sealed record ManifestDatasetVersion
{
    /// <summary>The dataset version.</summary>
    public required long Id { get; init; }

    /// <summary>The version number.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Publication time (UTC); null until published.</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>The version's bindings, by name.</summary>
    public required IReadOnlyList<ManifestBinding> Bindings { get; init; }

    /// <summary>The version's measures, by key.</summary>
    public required IReadOnlyList<ManifestMeasure> Measures { get; init; }
}

/// <summary>The contract's <c>ManifestBinding</c>: a binding with the identities it resolves through.</summary>
public sealed record ManifestBinding
{
    /// <summary>The binding's name: its role's key, or a time binding's field key.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>binding_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The bound dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The mapping set of a mapped binding; null otherwise.</summary>
    public required long? MappingSetId { get; init; }

    /// <summary>The bridge of a bridge binding; null otherwise.</summary>
    public required long? BridgeId { get; init; }
}

/// <summary>The contract's <c>ManifestMeasure</c>: a measure's key and whether it applies bridge factors.</summary>
public sealed record ManifestMeasure
{
    /// <summary>The measure key.</summary>
    public required string Key { get; init; }

    /// <summary>Whether the engine multiplies source rows by their bridge factor for the measure.</summary>
    public required bool AppliesBridgeFactors { get; init; }
}
