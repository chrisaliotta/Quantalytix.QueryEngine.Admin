namespace Quantalytix.QueryEngine.Admin.Application.Lookups;

/// <summary>Which picker list to read.</summary>
public sealed record LookupQuery
{
    /// <summary>A <c>lookup_kind</c> value, such as <c>dimension-versions</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The owning row's ID; required for child kinds, optional for <c>mapping-sets</c>, ignored otherwise.</summary>
    public long? ParentId { get; init; }
}
