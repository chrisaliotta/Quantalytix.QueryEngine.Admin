namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>ModelFormulaCheck</c>: what a formula would break in the saved model, and what it reads (OI-43).</summary>
public sealed record ModelFormulaCheck
{
    /// <summary>The errors, each over the characters it concerns; empty when the formula fits the model.</summary>
    public required IReadOnlyList<ModelFormulaError> Errors { get; init; }

    /// <summary>The metric keys the formula reads, in order of first use.</summary>
    public required IReadOnlyList<string> Reads { get; init; }

    /// <summary>The declared parameter keys the formula reads, in order of first use; never <c>periodDays</c> or <c>yearDays</c>.</summary>
    public required IReadOnlyList<string> Parameters { get; init; }

    /// <summary>The oldest formula language that has everything the formula uses.</summary>
    public required string RequiresLanguage { get; init; }
}

/// <summary>The contract's <c>ModelFormulaError</c>: one problem of a formula and where it is.</summary>
public sealed record ModelFormulaError
{
    /// <summary>What is wrong.</summary>
    public required string Message { get; init; }

    /// <summary>The zero-based offset where the problem starts.</summary>
    public required int Start { get; init; }

    /// <summary>The zero-based offset just past the problem.</summary>
    public required int End { get; init; }
}

/// <summary>The contract's <c>ModelFormulaCheckRequest</c>: the formula to check and the metric that owns it.</summary>
public sealed record ModelFormulaCheckRequest
{
    /// <summary>The formula.</summary>
    public required string Formula { get; init; }

    /// <summary>The key of the metric that owns the formula, so reading itself is reported; null for a new metric.</summary>
    public required string? SelfKey { get; init; }
}
