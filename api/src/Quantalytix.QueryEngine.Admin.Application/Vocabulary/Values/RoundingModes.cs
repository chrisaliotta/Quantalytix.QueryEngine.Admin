namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>rounding_mode</c> vocabulary: how the client rounds a measure's output to its output scale for display.</summary>
public static class RoundingModes
{
    /// <summary>Banker's rounding; ties go to the even neighbor.</summary>
    public const string HalfEven = "half_even";

    /// <summary>Ties round away from zero, in conventional financial style.</summary>
    public const string HalfUp = "half_up";

    /// <summary>Round toward negative infinity.</summary>
    public const string Down = "down";

    /// <summary>Round toward positive infinity.</summary>
    public const string Up = "up";

    /// <summary>Drop excess scale toward zero.</summary>
    public const string Truncate = "truncate";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "rounding_mode",
        Description = "How the client rounds a measure's output to its output scale for display.",
        Values =
        [
            new() { Value = HalfEven, Description = "Banker's rounding; ties go to the even neighbor." },
            new() { Value = HalfUp, Description = "Ties round away from zero, in conventional financial style." },
            new() { Value = Down, Description = "Round toward negative infinity." },
            new() { Value = Up, Description = "Round toward positive infinity." },
            new() { Value = Truncate, Description = "Drop excess scale toward zero." }
        ]
    };
}
