namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>dimension_purpose</c> vocabulary: whether a dimension classifies datasets or holds a report's rows.</summary>
public static class DimensionPurposes
{
    /// <summary>A dimension datasets bind through its roles and queries group and filter by.</summary>
    public const string Analytical = "analytical";

    /// <summary>The rows of the one report that owns it; it has no roles, and every bridge into it belongs to that report.</summary>
    public const string Report = "report";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "dimension_purpose",
        Key = "dimensionPurpose",
        Description = "Whether a dimension classifies datasets or holds a report's rows.",
        Values =
        [
            new() { Value = Analytical, Label = "Analytical", Description = "A dimension datasets bind through its roles and queries group and filter by." },
            new() { Value = Report, Label = "Report", Description = "The rows of the one report that owns it; it has no roles, and every bridge into it belongs to that report." }
        ]
    };
}
