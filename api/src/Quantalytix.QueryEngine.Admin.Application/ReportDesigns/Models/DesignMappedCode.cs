namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>One source code that the published version of a mapping set into the report dimension maps to a row.</summary>
public sealed record DesignMappedCode
{
    /// <summary>The mapping set.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>The source code.</summary>
    public required string SourceKey { get; init; }
}
