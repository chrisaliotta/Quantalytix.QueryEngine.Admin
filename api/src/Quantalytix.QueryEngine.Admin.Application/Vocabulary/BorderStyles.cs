namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>border_style</c> vocabulary: the rule a report style draws above or below its rows.</summary>
public static class BorderStyles
{
    /// <summary>No rule.</summary>
    public const string None = "none";

    /// <summary>A thin rule.</summary>
    public const string Thin = "thin";

    /// <summary>A medium rule.</summary>
    public const string Medium = "medium";

    /// <summary>A thick rule.</summary>
    public const string Thick = "thick";

    /// <summary>Two thin rules, as under a total.</summary>
    public const string Double = "double";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "border_style",
        Description = "The rule a report style draws above or below its rows.",
        Values =
        [
            new() { Value = None, Description = "No rule." },
            new() { Value = Thin, Description = "A thin rule." },
            new() { Value = Medium, Description = "A medium rule." },
            new() { Value = Thick, Description = "A thick rule." },
            new() { Value = Double, Description = "Two thin rules, as under a total." }
        ]
    };
}
