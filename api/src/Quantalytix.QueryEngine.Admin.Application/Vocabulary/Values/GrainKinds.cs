namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>grain_kind</c> vocabulary: what a dataset grain identifies.</summary>
public static class GrainKinds
{
    /// <summary>Identifies one logical detail row.</summary>
    public const string Row = "row";

    /// <summary>Identifies one independent temporal trajectory, excluding observation time.</summary>
    public const string Series = "series";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "grain_kind",
        Key = "grainKind",
        Description = "What a dataset grain identifies.",
        Values =
        [
            new() { Value = Row, Label = "Row", Description = "Identifies one logical detail row." },
            new() { Value = Series, Label = "Series", Description = "Identifies one independent temporal trajectory, excluding observation time." }
        ]
    };
}
