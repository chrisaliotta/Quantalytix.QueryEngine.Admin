using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A source profile with the source it picks for each variable.</summary>
public sealed record SourceProfile
{
    /// <summary>The profile row.</summary>
    public required SourceProfileRecord Profile { get; init; }

    /// <summary>The profile's picks, by variable ID.</summary>
    public required IReadOnlyList<SourceProfileVariableRecord> Picks { get; init; }
}
