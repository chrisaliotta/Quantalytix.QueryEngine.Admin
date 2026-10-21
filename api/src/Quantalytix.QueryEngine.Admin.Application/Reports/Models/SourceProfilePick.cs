namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The source a profile picks for one variable.</summary>
public sealed record SourceProfilePick
{
    /// <summary>The key of the model variable.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The picked source, one of the report's sources that supplies the variable.</summary>
    public required long ReportSourceId { get; init; }
}
