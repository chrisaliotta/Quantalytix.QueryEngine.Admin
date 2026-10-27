namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>subtotal_position</c> vocabulary: where a member row that shows values sits relative to its descendants.</summary>
public static class SubtotalPositions
{
    /// <summary>Before its descendants.</summary>
    public const string Above = "above";

    /// <summary>After its descendants.</summary>
    public const string Below = "below";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "subtotal_position",
        Key = "subtotalPosition",
        Description = "Where a member row that shows values sits relative to its descendants.",
        Values =
        [
            new() { Value = Above, Label = "Above", Description = "Before its descendants." },
            new() { Value = Below, Label = "Below", Description = "After its descendants." }
        ]
    };
}
