namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The editable identity attributes of a mapping set, as <c>PUT mapping-sets/{mappingSetId}</c> takes them.</summary>
public record MappingSetInput
{
    /// <summary>Namespace of the source vocabulary, such as <c>core_banking</c>; at most 128 characters.</summary>
    public required string SourceNamespace { get; init; }

    /// <summary>Human-readable name, at most 256 characters.</summary>
    public required string DisplayName { get; init; }
}
