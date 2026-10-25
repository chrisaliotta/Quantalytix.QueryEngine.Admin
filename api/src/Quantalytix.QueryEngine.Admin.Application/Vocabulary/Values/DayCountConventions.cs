namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>day_count_convention</c> vocabulary: how <c>YEARFRAC</c> counts the days of a period and of its year. A period runs from
/// its start up to, but not including, its end.
/// </summary>
public static class DayCountConventions
{
    /// <summary>Actual days over the days of the start date's year, 365 or 366.</summary>
    public const string ActualActual = "actual_actual";

    /// <summary>Actual days over 365.</summary>
    public const string Actual365 = "actual_365";

    /// <summary>Actual days over 360.</summary>
    public const string Actual360 = "actual_360";

    /// <summary>30/360 US (NASD): 30-day months, with February month ends and 31sts adjusted, over 360.</summary>
    public const string Thirty360Us = "thirty_360_us";

    /// <summary>30E/360 (Eurobond basis): 30-day months, with every 31st counted as the 30th, over 360.</summary>
    public const string ThirtyE360 = "thirty_e_360";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "day_count_convention",
        Description = "How YEARFRAC counts the days of a period and of its year.",
        Values =
        [
            new() { Value = ActualActual, Description = "Actual days over the days of the start date's year, 365 or 366." },
            new() { Value = Actual365, Description = "Actual days over 365." },
            new() { Value = Actual360, Description = "Actual days over 360." },
            new() { Value = Thirty360Us, Description = "30/360 US (NASD): 30-day months, with February month ends and 31sts adjusted, over 360." },
            new() { Value = ThirtyE360, Description = "30E/360 (Eurobond basis): 30-day months, with every 31st counted as the 30th, over 360." }
        ]
    };

    /// <summary>Whether text names a convention.</summary>
    /// <param name="text">A string literal or a parameter's allowed value.</param>
    /// <returns>True for one of the vocabulary's values.</returns>
    public static bool Contains(string text)
    {
        return Definition.Values.Any(value => value.Value == text);
    }
}
