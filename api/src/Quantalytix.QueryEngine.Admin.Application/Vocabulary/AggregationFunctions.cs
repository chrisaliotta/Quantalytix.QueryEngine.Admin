namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>aggregation_function</c> vocabulary: the aggregate function of an ordinary aggregate measure.</summary>
public static class AggregationFunctions
{
    /// <summary>Sum of the values.</summary>
    public const string Sum = "sum";

    /// <summary>Arithmetic mean of the values.</summary>
    public const string Avg = "avg";

    /// <summary>Smallest value.</summary>
    public const string Min = "min";

    /// <summary>Largest value.</summary>
    public const string Max = "max";

    /// <summary>Number of non-null values, or of rows when the measure has no value component.</summary>
    public const string Count = "count";

    /// <summary>Number of distinct non-null values.</summary>
    public const string CountDistinct = "count_distinct";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "aggregation_function",
        Description = "Aggregate function of an ordinary aggregate measure.",
        Values =
        [
            new() { Value = Sum, Description = "Sum of the values." },
            new() { Value = Avg, Description = "Arithmetic mean of the values." },
            new() { Value = Min, Description = "Smallest value." },
            new() { Value = Max, Description = "Largest value." },
            new() { Value = Count, Description = "Number of non-null values, or of rows when the measure has no value component." },
            new() { Value = CountDistinct, Description = "Number of distinct non-null values." }
        ]
    };
}
