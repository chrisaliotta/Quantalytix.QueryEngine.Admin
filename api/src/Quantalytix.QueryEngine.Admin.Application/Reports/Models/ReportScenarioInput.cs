namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A role whose members are alternatives a run of the report reads one at a time.</summary>
public sealed record ReportScenarioInput
{
    /// <summary>The role; requests name the scenario by its key.</summary>
    public required long DimensionRoleId { get; init; }
}
