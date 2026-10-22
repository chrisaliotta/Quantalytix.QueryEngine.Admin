using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>formula_parameters_resolve</c>: every <c>@name</c> the model's formulas and attribution expressions read is <c>@periodDays</c>,
/// <c>@yearDays</c> or a parameter the model declares, and every value is read where its type belongs. The context parameters and
/// number parameters are read only where a number belongs. Text, a string literal or a text parameter, is read only where text
/// belongs: compared with text by <c>=</c> or <c>&lt;&gt;</c>, as the operand or a value of an <c>IN</c> or <c>NOT IN</c> whose every
/// member is text, or as <c>YEARFRAC</c>'s argument, which is a day count convention in quotes or a text parameter whose allowed
/// values are all conventions. The engine evaluates formulas without checking types, and a parameter can be declared, retyped or
/// deleted after a formula reads it, so the rule checks every formula on every write.
/// </summary>
public static class FormulaParametersResolveRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "formula_parameters_resolve";

    /// <summary>Where text belongs, for the messages of text read anywhere else.</summary>
    private const string TextBelongs = "text is only compared with text by =, <> or IN, or passed to YEARFRAC";

    /// <summary>What a position of a formula takes.</summary>
    private enum Slot
    {
        /// <summary>A number: an operand of arithmetic or an ordering comparison, a function's argument, a CASE result.</summary>
        Number,

        /// <summary>Text: an operand compared with text, or YEARFRAC's argument.</summary>
        Text,

        /// <summary>A condition: a CASE condition, or an operand of AND, OR or NOT.</summary>
        Condition
    }

    /// <summary>Reports each name no parameter answers to, and each value read where its type does not belong, once per formula.</summary>
    /// <param name="model">The model, its metrics, attributions and parameters.</param>
    /// <returns>One violation per problem.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        var parameters = model.Parameters.ToDictionary(parameter => parameter.ParameterKey, StringComparer.Ordinal);
        return model.Formulas
            .OrderBy(formula => formula.Subject, StringComparer.Ordinal)
            .SelectMany(formula => Problems(ExpressionParser.Parse(formula.Expression), parameters)
                .Distinct(StringComparer.Ordinal)
                .Select(problem => new RuleViolation { Rule = Code, Message = $"{formula.Subject} {problem}." }));
    }

    /// <summary>A formula's problems: the names it reads that no parameter answers to, then the values read out of place.</summary>
    private static IEnumerable<string> Problems(Expression formula, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        return ExpressionTree.ParameterNames(formula)
            .Where(name => TypeOf(name, parameters) is null)
            .Select(name => $"reads @{name}, which is no parameter of the model, @periodDays or @yearDays")
            .Concat(Placement(formula, Slot.Number, parameters));
    }

    /// <summary>The values an expression in a position reads where their type does not belong.</summary>
    private static IEnumerable<string> Placement(Expression expression, Slot slot, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        return expression switch
        {
            ParameterReference reference => TypeOf(reference.Name, parameters) is { } type && type != slot ? [Misplaced($"@{reference.Name}", type, slot)] : [],
            Literal { Value: string text } => slot == Slot.Text ? [] : [Misplaced($"'{text}'", Slot.Text, slot)],
            FunctionCall { Function: ExpressionFunction.YearFrac } call => DayCount(call.Arguments[0], parameters),
            _ => Operands(expression, parameters).SelectMany(operand => Placement(operand.Expression, operand.Slot, parameters))
        };
    }

    /// <summary>Each operand of an expression with what its position takes; an equality or IN takes text only between text operands.</summary>
    private static IEnumerable<(Expression Expression, Slot Slot)> Operands(Expression expression, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        return expression switch
        {
            UnaryExpression { Operator: UnaryOperator.Not } negation => [(negation.Operand, Slot.Condition)],
            UnaryExpression negation => [(negation.Operand, Slot.Number)],
            BinaryExpression { Operator: BinaryOperator.And or BinaryOperator.Or } logical => [(logical.Left, Slot.Condition), (logical.Right, Slot.Condition)],
            BinaryExpression { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } equality =>
                [(equality.Left, Beside(equality.Right, parameters)), (equality.Right, Beside(equality.Left, parameters))],
            BinaryExpression binary => [(binary.Left, Slot.Number), (binary.Right, Slot.Number)],
            IsNullExpression isNull => [(isNull.Operand, Slot.Number)],
            InExpression membership => Members(membership, parameters),
            CaseExpression choice =>
            [
                .. choice.Branches.SelectMany(branch => new[] { (branch.When, Slot.Condition), (branch.Then, Slot.Number) }),
                .. choice.Else is { } otherwise ? [(otherwise, Slot.Number)] : Array.Empty<(Expression, Slot)>()
            ],
            FunctionCall call => call.Arguments.Select(argument => (argument, Slot.Number)),
            _ => []
        };
    }

    /// <summary>The operand and values of an IN, which are text positions when every one of them is text, and number positions otherwise.</summary>
    private static IEnumerable<(Expression Expression, Slot Slot)> Members(InExpression membership, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        Expression[] members = [membership.Operand, .. membership.Values];
        var slot = members.All(member => IsText(member, parameters)) ? Slot.Text : Slot.Number;
        return members.Select(member => (member, slot));
    }

    /// <summary>What an operand of = or &lt;&gt; takes: text beside text, and a number beside anything else.</summary>
    private static Slot Beside(Expression other, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        return IsText(other, parameters) ? Slot.Text : Slot.Number;
    }

    /// <summary>Whether an expression is text: a string literal or a text parameter.</summary>
    private static bool IsText(Expression expression, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        return expression is Literal { Value: string } || (expression is ParameterReference reference && TypeOf(reference.Name, parameters) == Slot.Text);
    }

    /// <summary>What a parameter holds: a number for the context parameters and number parameters, text for text parameters; null for no parameter.</summary>
    private static Slot? TypeOf(string name, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        if (FormulaContext.ParameterNames.Contains(name)) return Slot.Number;
        if (!parameters.TryGetValue(name, out var parameter)) return null;
        return parameter.ValueType == ParameterValueTypes.Text ? Slot.Text : Slot.Number;
    }

    /// <summary>The problem of YEARFRAC's argument, which names a day count convention; a name no parameter answers to is reported once, as such.</summary>
    private static IEnumerable<string> DayCount(Expression argument, IReadOnlyDictionary<string, ModelParameter> parameters)
    {
        return argument switch
        {
            Literal { Value: string text } => DayCountConventions.Contains(text) ? [] : [$"calls YEARFRAC with '{text}', which is no day count convention"],
            ParameterReference reference when TypeOf(reference.Name, parameters) is null => [],
            ParameterReference reference when TypeOf(reference.Name, parameters) == Slot.Text => parameters[reference.Name].AllowedValues is { Count: > 0 } allowed
                && allowed.All(DayCountConventions.Contains)
                    ? []
                    : [$"calls YEARFRAC with @{reference.Name}, which may hold text that is no day count convention; its allowed values must all be conventions"],
            _ => [$"calls YEARFRAC with something other than a day count convention, which it takes in quotes, such as '{DayCountConventions.Actual360}', or from a text parameter whose allowed values are conventions"]
        };
    }

    /// <summary>The message of a value read where its type does not belong.</summary>
    private static string Misplaced(string value, Slot type, Slot slot)
    {
        return (type, slot) switch
        {
            (Slot.Number, Slot.Text) => $"compares {value}, a number, with text",
            (Slot.Number, _) => $"reads {value}, a number, where a condition belongs",
            (_, Slot.Number) => $"reads {value}, which is text, where a number belongs; {TextBelongs}",
            _ => $"reads {value}, which is text, where a condition belongs; {TextBelongs}"
        };
    }
}
