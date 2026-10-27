namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>ragged_behavior</c> vocabulary: what happens when a requested level, depth or ancestor cannot be resolved for a member.</summary>
public static class RaggedBehaviors
{
    /// <summary>Group the member under a null dimension value (the default).</summary>
    public const string Null = "null";

    /// <summary>Keep the closest or self member.</summary>
    public const string Self = "self";

    /// <summary>Exclude the member from the grouping result.</summary>
    public const string Exclude = "exclude";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "ragged_behavior",
        Key = "raggedBehavior",
        Description = "What happens when a requested level, depth or ancestor cannot be resolved for a member.",
        Values =
        [
            new() { Value = Null, Label = "Null", Description = "Group the member under a null dimension value (the default)." },
            new() { Value = Self, Label = "Self", Description = "Keep the closest or self member." },
            new() { Value = Exclude, Label = "Exclude", Description = "Exclude the member from the grouping result." }
        ]
    };
}
