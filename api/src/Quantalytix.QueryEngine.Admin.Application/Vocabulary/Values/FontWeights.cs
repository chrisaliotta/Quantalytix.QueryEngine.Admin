namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>font_weight</c> vocabulary: how heavy a report style sets its rows' text.</summary>
public static class FontWeights
{
    /// <summary>Regular text, CSS weight 400.</summary>
    public const string Normal = "normal";

    /// <summary>CSS weight 500.</summary>
    public const string Medium = "medium";

    /// <summary>CSS weight 600.</summary>
    public const string Semibold = "semibold";

    /// <summary>CSS weight 700.</summary>
    public const string Bold = "bold";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "font_weight",
        Key = "fontWeight",
        Description = "How heavy a report style sets its rows' text.",
        Values =
        [
            new() { Value = Normal, Label = "Normal", Description = "Regular text, CSS weight 400." },
            new() { Value = Medium, Label = "Medium", Description = "CSS weight 500." },
            new() { Value = Semibold, Label = "Semibold", Description = "CSS weight 600." },
            new() { Value = Bold, Label = "Bold", Description = "CSS weight 700." }
        ]
    };
}
