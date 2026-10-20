using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// A dimension binding with the role it binds its field to and its time extension, joined from <c>qe_dimension_binding</c>,
/// <c>qe_dimension_role</c> and <c>qe_time_binding</c>.
/// </summary>
public sealed record BindingDetail
{
    /// <summary>The binding.</summary>
    public required DimensionBindingRecord Binding { get; init; }

    /// <summary>The role of a direct, mapped, range or bridge binding; null for a <c>time</c> binding.</summary>
    public DimensionRoleRecord? Role { get; init; }

    /// <summary>The time extension of a <c>time</c> binding; null for every other kind.</summary>
    public TimeBindingRecord? TimeBinding { get; init; }
}
