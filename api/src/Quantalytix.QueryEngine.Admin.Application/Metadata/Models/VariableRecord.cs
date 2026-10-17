namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_variable</c> row: a logical input of a model version, which a source profile fills from one of its sources.</summary>
public sealed record VariableRecord
{
    /// <summary>Primary key.</summary>
    public required long VariableId { get; init; }

    /// <summary>The owning model version.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>Key, unique among the version's variables and calculated measures.</summary>
    public required string VariableKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>average_balance</c>.</summary>
    public required string VariableRole { get; init; }

    /// <summary>The report-item member a <c>descendants</c> or <c>exact</c> variable reads.</summary>
    public string? TargetMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value.</summary>
    public required string SelectionScope { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value.</summary>
    public required string MissingBehavior { get; init; }
}
