namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A source profile of a draft model version with its complete pick list, as its create and edit endpoints take it.</summary>
public sealed record SourceProfileInput
{
    /// <summary>Key, unique among the version's profiles.</summary>
    public required string ProfileKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether report requests may run under the profile.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The source the profile picks for each variable; each variable appears once.</summary>
    public required IReadOnlyList<SourceProfilePick> Picks { get; init; }
}
