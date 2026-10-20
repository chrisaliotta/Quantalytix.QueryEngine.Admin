namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The member of the report's dimension one variable reads at.</summary>
public sealed record ReportVariableTargetInput
{
    /// <summary>The key of the model variable.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The member of the report's dimension the variable reads.</summary>
    public required string TargetMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value: <c>exact</c> or <c>descendants</c>.</summary>
    public required string SelectionScope { get; init; }
}
