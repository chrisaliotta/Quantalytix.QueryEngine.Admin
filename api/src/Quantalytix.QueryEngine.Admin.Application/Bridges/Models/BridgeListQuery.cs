namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Which bridges to list.</summary>
public sealed record BridgeListQuery
{
    /// <summary>A <c>dimension_purpose</c> value that narrows the list to bridges of that purpose; null lists every bridge.</summary>
    public string? Purpose { get; init; }
}
