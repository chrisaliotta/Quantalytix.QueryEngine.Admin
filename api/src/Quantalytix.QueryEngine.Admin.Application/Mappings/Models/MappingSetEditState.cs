namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingSetEditState</c>: the mapping set change-set aggregate as the editor shows it.</summary>
public sealed record MappingSetEditState
{
    /// <summary>The mapping set with its versions and bindings.</summary>
    public required MappingSetListItem Set { get; init; }

    /// <summary>The live version's content.</summary>
    public required MappingVersionContent Content { get; init; }
}
