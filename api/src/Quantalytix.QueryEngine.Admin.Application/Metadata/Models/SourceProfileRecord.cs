namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_source_profile</c> row: a named choice of one source per variable a report reads.</summary>
public sealed record SourceProfileRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationSourceProfileId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>Key, unique among the report's profiles.</summary>
    public required string ProfileKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether report requests may run under the profile.</summary>
    public required bool IsEnabled { get; init; }
}
