namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>What <see cref="FilterNodes"/> found in a structured filter.</summary>
public sealed record FilterInspection
{
    /// <summary>Whether the filter is a well-formed <c>filterNode</c>.</summary>
    public required bool IsValid { get; init; }

    /// <summary>The first shape error, naming its JSON path; null when the filter is valid.</summary>
    public string? Error { get; init; }

    /// <summary>The filter's typed operands, in reading order; literals are not listed.</summary>
    public required IReadOnlyList<FilterOperand> Operands { get; init; }
}
