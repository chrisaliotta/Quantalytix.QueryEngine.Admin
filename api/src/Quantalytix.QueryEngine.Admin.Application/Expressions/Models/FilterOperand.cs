namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>One typed operand of a structured filter, such as <c>{ "type": "dimension", "id": "region" }</c>.</summary>
public sealed record FilterOperand
{
    /// <summary>The operand type: <c>field</c>, <c>measure</c>, <c>dimension</c> or <c>parameter</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The key the operand names.</summary>
    public required string Id { get; init; }
}
