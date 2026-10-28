namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>ModelReportUse</c>: a report that reads the model (<c>qe_report.calculation_model_id</c>, OI-44).</summary>
public sealed record ModelReportUse
{
    /// <summary>The report's id.</summary>
    public required long ReportId { get; init; }

    /// <summary>The report's key.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The report's name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the engine offers the report, and so reads the model through it.</summary>
    public required bool IsEnabled { get; init; }
}
