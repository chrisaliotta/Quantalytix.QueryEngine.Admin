namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>unmapped_behavior</c> vocabulary: what happens to a source value that no mapping covers.</summary>
public static class UnmappedBehaviors
{
    /// <summary>Expose a null dimension member (the default).</summary>
    public const string Null = "null";

    /// <summary>The row does not contribute through the binding.</summary>
    public const string Exclude = "exclude";

    /// <summary>Route the row to the binding's unknown member.</summary>
    public const string Unknown = "unknown";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "unmapped_behavior",
        Description = "What happens to a source value that no mapping covers.",
        Values =
        [
            new() { Value = Null, Description = "Expose a null dimension member (the default)." },
            new() { Value = Exclude, Description = "The row does not contribute through the binding." },
            new() { Value = Unknown, Description = "Route the row to the binding's unknown member." }
        ]
    };
}
