namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A variable of a draft model version, as its create and edit endpoints take it.</summary>
public sealed record VariableInput
{
    /// <summary>Key, unique among the version's variables and calculated measures.</summary>
    public required string VariableKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>average_balance</c>.</summary>
    public required string VariableRole { get; init; }

    /// <summary>The report-item member a <c>descendants</c> or <c>exact</c> variable reads; null for <c>all_mapped</c>.</summary>
    public string? TargetMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value.</summary>
    public required string SelectionScope { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value.</summary>
    public required string MissingBehavior { get; init; }
}
