using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The rule findings of mapping set and bridge content, shared by both kinds' rules and read models.</summary>
public static class MappingViolations
{
    /// <summary>A finding naming the item at fault by its stored id's digits, as every rule does.</summary>
    /// <param name="code">The rule code.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="area">The editor area to fix it in, a <see cref="MappingAreas"/> value.</param>
    /// <param name="itemId">The entry or mapping at fault; null for none.</param>
    /// <param name="itemKey">The source member of a per-source finding; null for none.</param>
    /// <param name="filter">Search text that narrows the table to the rows at fault; null for none.</param>
    /// <returns>The violation.</returns>
    public static RuleViolation Of(string code, string message, string area, long? itemId = null, string? itemKey = null, string? filter = null)
    {
        return new RuleViolation
        {
            Code = code,
            Message = message,
            Target = new ViolationTarget { Area = area, ItemId = itemId is { } id ? TemporaryIds.PresentStored(id) : null, ItemKey = itemKey, Filter = filter }
        };
    }

    /// <summary>Findings whose target items are written as the read model writes its ids, so a preview names created items by their temporary ids.</summary>
    /// <param name="violations">The findings, naming items by their digits.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The findings with their items presented.</returns>
    public static IReadOnlyList<RuleViolation> Presented(IReadOnlyList<RuleViolation> violations, Func<long, string> presentId)
    {
        return
        [
            .. violations.Select(violation => violation.Target is { ItemId: { } itemId } target
                ? violation with { Target = target with { ItemId = presentId(long.Parse(itemId, NumberStyles.None, CultureInfo.InvariantCulture)) } }
                : violation)
        ];
    }
}
