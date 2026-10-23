namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report_scenario</c> row: a dimension role whose members are alternatives a run of the report reads one at a time, such
/// as plan versions or interest rate shocks.
/// </summary>
public sealed record ReportScenarioRecord
{
    /// <summary>The owning report; first part of the primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>The role; second part of the primary key. Requests name the scenario by its key.</summary>
    public required long RoleId { get; init; }
}
