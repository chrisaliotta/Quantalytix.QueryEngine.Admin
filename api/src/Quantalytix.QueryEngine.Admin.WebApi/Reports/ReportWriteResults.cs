using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>Builds the 422 answers of report writes that the report rules refuse.</summary>
internal static class ReportWriteResults
{
    /// <summary>The answer to a write that would leave an enabled report breaking its rules; nothing was written.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="violations">What the write would break.</param>
    /// <returns>The 422 result.</returns>
    public static ObjectResult Rejected(HttpContext httpContext, long reportId, IReadOnlyList<RuleViolation> violations)
    {
        return RuleViolationResults.Create(httpContext, $"Report {reportId} is enabled, and the change would break its rules; nothing was written.", violations);
    }

    /// <summary>The answer to enabling a report that breaks its rules; it stays disabled.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="reportId">The report.</param>
    /// <param name="violations">What the report breaks.</param>
    /// <returns>The 422 result.</returns>
    public static ObjectResult NotEnabled(HttpContext httpContext, long reportId, IReadOnlyList<RuleViolation> violations)
    {
        return RuleViolationResults.Create(httpContext, $"Report {reportId} breaks its rules, so it stays disabled.", violations);
    }
}
