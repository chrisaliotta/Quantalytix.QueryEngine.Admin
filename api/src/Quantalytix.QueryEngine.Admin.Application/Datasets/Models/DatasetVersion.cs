using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Measures;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetVersion</c>: a version's whole editable content, the state of its change set.</summary>
public sealed record DatasetVersion : DatasetVersionSummary
{
    /// <summary>A summary with the version's content.</summary>
    /// <param name="summary">The summary.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="fields">The fields.</param>
    /// <param name="grains">The grains.</param>
    /// <param name="bindings">The bindings.</param>
    /// <param name="measures">The measures.</param>
    /// <param name="dependents">What reads each item.</param>
    [SetsRequiredMembers]
    public DatasetVersion(
        DatasetVersionSummary summary,
        DatasetVersionSettings settings,
        IReadOnlyList<DatasetField> fields,
        IReadOnlyList<DatasetGrain> grains,
        IReadOnlyList<DatasetBinding> bindings,
        IReadOnlyList<DatasetMeasure> measures,
        IReadOnlyDictionary<string, IReadOnlyList<DatasetDependent>> dependents)
        : base(summary)
    {
        Settings = settings;
        Fields = fields;
        Grains = grains;
        Bindings = bindings;
        Measures = measures;
        Dependents = dependents;
    }

    /// <summary>The version row's settings.</summary>
    public required DatasetVersionSettings Settings { get; init; }

    /// <summary>The fields.</summary>
    public required IReadOnlyList<DatasetField> Fields { get; init; }

    /// <summary>The grains.</summary>
    public required IReadOnlyList<DatasetGrain> Grains { get; init; }

    /// <summary>The bindings.</summary>
    public required IReadOnlyList<DatasetBinding> Bindings { get; init; }

    /// <summary>The measures.</summary>
    public required IReadOnlyList<DatasetMeasure> Measures { get; init; }

    /// <summary>What reads each item, by the item's id.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<DatasetDependent>> Dependents { get; init; }
}
