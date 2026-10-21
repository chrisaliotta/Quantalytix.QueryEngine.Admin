namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// A new design source, as <c>POST report-versions/{versionId}/design/sources</c> takes it: its fixed key, its editable
/// attributes, and optionally a bridge whose published row mappings its own bridge starts with.
/// </summary>
public sealed record NewDesignSourceInput : DesignSourceInput
{
    /// <summary>Stable key, fixed once created; the source's bridge and variable sources take it in their keys.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// A bridge from the bound dimension whose published entries into members of the report dimension the source's bridge copies;
    /// null for a source without mappings yet.
    /// </summary>
    public long? CopyMappingsFromBridgeId { get; init; }
}
