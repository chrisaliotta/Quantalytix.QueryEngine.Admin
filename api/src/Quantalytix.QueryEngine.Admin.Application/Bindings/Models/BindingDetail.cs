using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>A dimension binding with its time extension, joined from <c>qe_dimension_binding</c> and <c>qe_time_binding</c>.</summary>
public sealed record BindingDetail
{
    /// <summary>The binding.</summary>
    public required DimensionBindingRecord Binding { get; init; }

    /// <summary>The time extension of a <c>time</c> binding; null for every other kind.</summary>
    public TimeBindingRecord? TimeBinding { get; init; }

    /// <summary>
    /// The bridge that owns the binding's bridge version, so an editor can offer that bridge's versions; null when the
    /// binding has no bridge version.
    /// </summary>
    public long? BridgeId { get; init; }
}
