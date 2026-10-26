using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FluentValidation.Internal;

namespace Quantalytix.QueryEngine.Admin.Api.Errors;

/// <summary>
/// Names validated properties by their camelCase JSON path, so FluentValidation reports <c>components[0].fieldId</c> and
/// a client can match each error to the JSON value it sent.
/// </summary>
public static class JsonPropertyPaths
{
    /// <summary>
    /// A FluentValidation property-name resolver: the rule's member chain, each segment in camelCase. FluentValidation adds
    /// collection indexers and parent paths itself.
    /// </summary>
    /// <param name="type">The validated type.</param>
    /// <param name="member">The validated member, when the rule names one.</param>
    /// <param name="expression">The rule's expression, when there is one.</param>
    /// <returns>The camelCase path, or null when the rule names no member.</returns>
    public static string? Resolve(Type type, MemberInfo? member, LambdaExpression? expression)
    {
        var path = member?.Name;
        if (expression is not null)
        {
            var chain = PropertyChain.FromExpression(expression);
            if (chain.Count > 0) path = chain.ToString();
        }

        if (path is null) return null;
        return string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
