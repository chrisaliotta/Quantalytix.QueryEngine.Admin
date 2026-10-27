using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Where to take the user to fix a violation: an area of the editor, optionally narrowed to one item or a search.</summary>
public sealed record ViolationTarget
{
    /// <summary>A module-specific area, such as a dataset tab, a dimension tab, a release manifest row or a report section.</summary>
    public required string Area { get; init; }

    /// <summary>The offending item; omitted when the violation names no single item.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ItemId { get; init; }

    /// <summary>The offending item's key; omitted when the violation names none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ItemKey { get; init; }

    /// <summary>Search text that narrows a table to the offending rows; omitted when there is none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filter { get; init; }
}
