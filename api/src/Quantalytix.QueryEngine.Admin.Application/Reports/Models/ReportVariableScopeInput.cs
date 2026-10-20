namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The member of the report's dimension whose branch one variable reads.</summary>
public sealed record ReportVariableScopeInput
{
    /// <summary>The key of the model variable.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The member of the report's dimension the variable is scoped to.</summary>
    public required string ScopeMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value: <c>exact</c> or <c>descendants</c>.</summary>
    public required string SelectionScope { get; init; }
}
