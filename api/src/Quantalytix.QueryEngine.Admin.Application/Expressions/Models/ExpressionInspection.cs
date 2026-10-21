using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>What the query engine's expression parser found in an expression text.</summary>
public sealed record ExpressionInspection
{
    /// <summary>Whether the text is a valid expression language 1.1 expression; language 1.0 is the same without <c>POWER</c>.</summary>
    public required bool IsValid { get; init; }

    /// <summary>The engine's syntax error message, ending "at character N."; null when the text is valid.</summary>
    public string? Error { get; init; }

    /// <summary>The zero-based character offset of the syntax error; null when the text is valid.</summary>
    public int? Position { get; init; }

    /// <summary>The distinct keys of the <c>[f.key]</c> field references, in reading order.</summary>
    public required IReadOnlyList<string> FieldKeys { get; init; }

    /// <summary>The distinct keys of the <c>[m.key]</c> measure references, in reading order.</summary>
    public required IReadOnlyList<string> MeasureKeys { get; init; }

    /// <summary>The distinct keys of the <c>[d.key]</c> dimension member references, in reading order.</summary>
    public required IReadOnlyList<string> DimensionKeys { get; init; }

    /// <summary>The distinct names of the <c>@name</c> request parameter references, without the <c>@</c>, in reading order.</summary>
    public required IReadOnlyList<string> ParameterNames { get; init; }

    /// <summary>The distinct functions the expression calls, in reading order.</summary>
    public required IReadOnlyList<ExpressionFunction> Functions { get; init; }
}
