namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_variable_scope</c> row: the member of the report dimension whose branch of the report one variable reads.</summary>
public sealed record ReportVariableScopeRecord
{
    /// <summary>The owning report version; first part of the primary key.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The key of the model's variable; second part of the primary key.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The member of the report dimension the variable is scoped to.</summary>
    public required string ScopeMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value other than <c>all_mapped</c>, which a variable without a scope reads.</summary>
    public required string SelectionScope { get; init; }
}
