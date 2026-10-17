namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_source_profile_variable</c> row: the source one profile picks for one variable.</summary>
public sealed record SourceProfileVariableRecord
{
    /// <summary>The profile; first part of the primary key.</summary>
    public required long SourceProfileId { get; init; }

    /// <summary>The variable; second part of the primary key.</summary>
    public required long VariableId { get; init; }

    /// <summary>The picked source, one of the variable's.</summary>
    public required long VariableSourceId { get; init; }
}
