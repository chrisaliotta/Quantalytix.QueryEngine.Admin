using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>ReportSourceRule</c>: an item, or every item under a member, that adds into a row with a factor.</summary>
public sealed record ReportSourceRule
{
    /// <summary>The key of a member of the source role's dimension.</summary>
    public required string ItemKey { get; init; }

    /// <summary>A <c>source_selection</c> value.</summary>
    public required string Selection { get; init; }

    /// <summary>The only number a rule carries; -1 on an item already included excludes it (OI-52).</summary>
    public required decimal Factor { get; init; }
}

/// <summary>The contract's <c>ReportSourceVariable</c>: the dataset measure that supplies one model variable from a source.</summary>
public sealed record ReportSourceVariable
{
    /// <summary>The model variable.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The dataset measure.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>A filter over the dataset's filterable fields, applied before aggregation; null for none.</summary>
    public required FilterNode? Filter { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value replacing the variable's own for this source; null keeps the variable's.</summary>
    public required string? MissingBehavior { get; init; }
}

/// <summary>The contract's <c>ReportSourceInput</c>: a source's identity; dataset, method, role and field are fixed once created.</summary>
public record ReportSourceInput
{
    /// <summary>The source key, unique in the report.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name, which a bridge source's bridge takes too.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dataset the source reads.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The dataset field, with a time binding, the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }

    /// <summary>A <c>report_mapping_method</c> value.</summary>
    public required string Method { get; init; }

    /// <summary>The role a bridge source's items come from; null for a direct source.</summary>
    public required long? RoleId { get; init; }

    /// <summary>The field whose values are row keys, for a direct source; null for a bridge source.</summary>
    public required string? FieldKey { get; init; }
}

/// <summary>The contract's <c>ReportSource</c>: a source with its report bridge, variables, rules and left-out items.</summary>
public sealed record ReportSource : ReportSourceInput
{
    /// <summary>A source of its identity and content.</summary>
    /// <param name="input">The identity.</param>
    /// <param name="bridgeId">The report bridge it owns.</param>
    /// <param name="variables">The variables it supplies.</param>
    /// <param name="rules">Its rules by row key.</param>
    /// <param name="leftOut">Its left-out items.</param>
    [SetsRequiredMembers]
    public ReportSource(
        ReportSourceInput input,
        long? bridgeId,
        IReadOnlyList<ReportSourceVariable> variables,
        IReadOnlyDictionary<string, IReadOnlyList<ReportSourceRule>> rules,
        IReadOnlyList<string> leftOut) : base(input)
    {
        BridgeId = bridgeId;
        Variables = variables;
        Rules = rules;
        LeftOut = leftOut;
    }

    /// <summary>The report bridge a bridge source owns; null for a direct source and before a new bridge source is saved.</summary>
    public required long? BridgeId { get; init; }

    /// <summary>The variables the source supplies.</summary>
    public required IReadOnlyList<ReportSourceVariable> Variables { get; init; }

    /// <summary>A bridge source's rules by row key.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<ReportSourceRule>> Rules { get; init; }

    /// <summary>A bridge source's items left out on purpose, which coverage does not count as gaps.</summary>
    public required IReadOnlyList<string> LeftOut { get; init; }
}
