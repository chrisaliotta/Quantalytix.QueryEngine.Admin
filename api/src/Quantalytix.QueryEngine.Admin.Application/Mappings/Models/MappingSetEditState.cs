namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingSetEditState</c>: the mapping set change-set aggregate as the editor shows it.</summary>
public sealed record MappingSetEditState
{
    /// <summary>The mapping set with its versions, bindings and pins.</summary>
    public required MappingSetDetail Set { get; init; }

    /// <summary>The live version's content.</summary>
    public required MappingVersionContent Content { get; init; }
}
