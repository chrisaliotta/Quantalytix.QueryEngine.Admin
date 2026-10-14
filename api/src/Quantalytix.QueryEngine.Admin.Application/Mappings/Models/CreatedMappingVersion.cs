using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>A new draft mapping version, with the source keys a clone could not carry over.</summary>
public sealed record CreatedMappingVersion
{
    /// <summary>The new draft.</summary>
    public required MappingVersionRecord Version { get; init; }

    /// <summary>
    /// The source keys of the cloned entries whose member key no member of the new draft's dimension version has, in
    /// ordinal order; empty for an empty draft.
    /// </summary>
    public required IReadOnlyList<string> DroppedSourceKeys { get; init; }
}
