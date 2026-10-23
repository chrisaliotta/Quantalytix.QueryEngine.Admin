namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_source_profile_variable</c> row: the source one profile picks for one variable.</summary>
public sealed record SourceProfileVariableRecord
{
    /// <summary>The profile; first part of the primary key.</summary>
    public required long CalculationSourceProfileId { get; init; }

    /// <summary>The key of the model variable; second part of the primary key.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The picked report source, which supplies the variable.</summary>
    public required long ReportSourceId { get; init; }
}
