namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>The distinct non-null values a field holds, as text in ordinal order, up to a limit.</summary>
public sealed record FieldValues
{
    /// <summary>The values, at most the limit of the read.</summary>
    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>Whether the values are all the field holds; false when the field holds more than the limit.</summary>
    public required bool IsComplete { get; init; }
}
