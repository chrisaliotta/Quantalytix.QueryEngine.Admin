namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>negative_weight_behavior</c> vocabulary: how a weighted measure treats negative statistical weights.</summary>
public static class NegativeWeightBehaviors
{
    /// <summary>Signed weights take part as they are (the default).</summary>
    public const string Allow = "allow";

    /// <summary>Negative-weight pairs are omitted.</summary>
    public const string Exclude = "exclude";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "negative_weight_behavior",
        Key = "negativeWeightBehavior",
        Description = "How a weighted measure treats negative statistical weights.",
        Values =
        [
            new() { Value = Allow, Label = "Allow", Description = "Signed weights take part as they are (the default)." },
            new() { Value = Exclude, Label = "Exclude", Description = "Negative-weight pairs are omitted." }
        ]
    };
}
