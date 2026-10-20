namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_variable</c> row: a logical input of a model version, which each report fills from one of its own sources.</summary>
public sealed record VariableRecord
{
    /// <summary>Primary key.</summary>
    public required long VariableId { get; init; }

    /// <summary>The owning model version.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>Key, unique among the version's variables and calculated measures; reports name the variable by it.</summary>
    public required string VariableKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>average_balance</c>.</summary>
    public required string VariableRole { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value.</summary>
    public required string MissingBehavior { get; init; }

    /// <summary>Presentation hint such as <c>currency</c>, which a report column shows unless it overrides it.</summary>
    public required string DisplayFormat { get; init; }

    /// <summary>Optional unit, such as a currency code.</summary>
    public string? UnitKey { get; init; }
}
