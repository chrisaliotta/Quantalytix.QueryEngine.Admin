namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>empty_set_behavior</c> vocabulary: what a measure returns when no inputs are eligible.</summary>
public static class EmptySetBehaviors
{
    /// <summary>No eligible inputs produces null (the default).</summary>
    public const string Null = "null";

    /// <summary>No eligible inputs produces zero.</summary>
    public const string Zero = "zero";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "empty_set_behavior",
        Key = "emptySetBehavior",
        Description = "What a measure returns when no inputs are eligible.",
        Values =
        [
            new() { Value = Null, Label = "Null", Description = "No eligible inputs produces null (the default)." },
            new() { Value = Zero, Label = "Zero", Description = "No eligible inputs produces zero." }
        ]
    };
}
