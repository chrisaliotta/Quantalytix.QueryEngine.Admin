namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>classification_as_of</c> vocabulary: which time a variable source classifies its rows into report items at.</summary>
public static class ClassificationTimes
{
    /// <summary>Each row is classified as of its own observation time.</summary>
    public const string ObservationTime = "observation_time";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "classification_as_of",
        Description = "Which time a variable source classifies its rows into report items at.",
        Values =
        [
            new() { Value = ObservationTime, Description = "Each row is classified as of its own observation time." }
        ]
    };
}
