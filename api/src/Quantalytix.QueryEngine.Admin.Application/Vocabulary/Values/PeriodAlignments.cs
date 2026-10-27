namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>default_period_alignment</c> vocabulary: the point in time a binding's dimension attributes are resolved at.</summary>
public static class PeriodAlignments
{
    /// <summary>Use dimension attributes from the selected fact observation.</summary>
    public const string Observation = "observation";

    /// <summary>Resolve dimension attributes at the requested period start.</summary>
    public const string Start = "start";

    /// <summary>Resolve dimension attributes at the requested period end.</summary>
    public const string End = "end";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "default_period_alignment",
        Key = "periodAlignment",
        Description = "The point in time a binding's dimension attributes are resolved at.",
        Values =
        [
            new() { Value = Observation, Label = "Observation", Description = "Use dimension attributes from the selected fact observation." },
            new() { Value = Start, Label = "Start", Description = "Resolve dimension attributes at the requested period start." },
            new() { Value = End, Label = "End", Description = "Resolve dimension attributes at the requested period end." }
        ]
    };
}
