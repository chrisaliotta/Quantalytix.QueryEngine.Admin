namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>range_data_type</c> vocabulary: the boundary type of a range dimension's bands.</summary>
public static class RangeDataTypes
{
    /// <summary>Bands use decimal boundaries.</summary>
    public const string Decimal = "decimal";

    /// <summary>Bands use date boundaries.</summary>
    public const string Date = "date";

    /// <summary>Bands use datetime boundaries.</summary>
    public const string Datetime = "datetime";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "range_data_type",
        Description = "Boundary type of a range dimension's bands.",
        Values =
        [
            new() { Value = Decimal, Description = "Bands use decimal boundaries." },
            new() { Value = Date, Description = "Bands use date boundaries." },
            new() { Value = Datetime, Description = "Bands use datetime boundaries." }
        ]
    };
}
