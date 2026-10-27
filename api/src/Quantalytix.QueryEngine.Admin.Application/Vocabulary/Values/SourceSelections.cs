namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>source_selection</c> vocabulary: which members of its source dimension a bridge mapping maps.</summary>
public static class SourceSelections
{
    /// <summary>The source member alone.</summary>
    public const string Member = "member";

    /// <summary>The source member and every member under it, including members added under it later.</summary>
    public const string Descendants = "descendants";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "source_selection",
        Key = "sourceSelection",
        Description = "Which members of its source dimension a bridge mapping maps.",
        Values =
        [
            new() { Value = Member, Label = "Member", Description = "The source member alone." },
            new() { Value = Descendants, Label = "Descendants", Description = "The source member and every member under it, including members added under it later." }
        ]
    };
}
