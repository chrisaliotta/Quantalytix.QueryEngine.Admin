namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_variable_target</c> row: the member of the report dimension a report version reads one variable at.</summary>
public sealed record ReportVariableTargetRecord
{
    /// <summary>The owning report version; first part of the primary key.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The key of the model's variable; second part of the primary key.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The member of the report dimension the variable reads.</summary>
    public required string TargetMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value other than <c>all_mapped</c>, which a variable without a target reads.</summary>
    public required string SelectionScope { get; init; }
}
