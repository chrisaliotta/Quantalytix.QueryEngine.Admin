namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_metric_attribution</c> row: how a change in one metric of a model splits into the effects of its drivers, and which
/// variable weighs child rows and breakout members when a ratio's change splits into rate, mix and interaction.
/// </summary>
public sealed record MetricAttributionRecord
{
    /// <summary>The owning model; first part of the primary key.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>The key of the variable or calculated measure attributed; second part of the primary key.</summary>
    public required string MetricKey { get; init; }

    /// <summary>The expression the drivers are switched in; null means the calculated measure's own formula.</summary>
    public string? ExpressionText { get; init; }

    /// <summary>The variable that weighs members when the metric's change splits across them; null for no member contributions.</summary>
    public string? WeightVariableKey { get; init; }
}
