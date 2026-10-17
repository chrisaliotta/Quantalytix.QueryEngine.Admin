namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A release's complete report pins, which replace the current ones.</summary>
public sealed record ReleaseReportsInput
{
    /// <summary>One pin per report the release serves.</summary>
    public required IReadOnlyList<ReleaseReportInput> Reports { get; init; }
}
