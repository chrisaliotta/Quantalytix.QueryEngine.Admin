namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>duration_unit</c> vocabulary: the unit interval durations are weighted by.</summary>
public static class DurationUnits
{
    /// <summary>Weight by calendar-day count; suits date-based daily averages.</summary>
    public const string CalendarDay = "calendar_day";

    /// <summary>Weight by elapsed seconds between timestamp boundaries; relevant across daylight-saving shifts.</summary>
    public const string ElapsedSecond = "elapsed_second";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "duration_unit",
        Key = "durationUnit",
        Description = "The unit interval durations are weighted by.",
        Values =
        [
            new() { Value = CalendarDay, Label = "Calendar day", Description = "Weight by calendar-day count; suits date-based daily averages." },
            new() { Value = ElapsedSecond, Label = "Elapsed second", Description = "Weight by elapsed seconds between timestamp boundaries; relevant across daylight-saving shifts." }
        ]
    };
}
