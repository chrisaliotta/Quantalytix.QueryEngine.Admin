using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The model change-set aggregate held in memory: the model's content and what the gate checks it against.</summary>
public sealed record ModelSnapshot
{
    /// <summary>The model's content, as the operations so far leave it.</summary>
    public required ModelGraph Content { get; init; }

    /// <summary>The reports over the model and the violations the stored model has.</summary>
    public required ModelSurroundings Surroundings { get; init; }
}

/// <summary>
/// What a model change set reads once, outside the model: every report over the model with what the report rules check it against,
/// and the violations the stored model already has, which do not refuse an edit (OI-42).
/// </summary>
public sealed record ModelSurroundings
{
    /// <summary>The reports that read the model, enabled or not, each as it stands, by report key.</summary>
    public required IReadOnlyList<ReportGraph> Reports { get; init; }

    /// <summary>The active release and dimension versions the reports are checked against; null when no report reads the model.</summary>
    public required ReportScope? Scope { get; init; }

    /// <summary>The violations of the model as stored.</summary>
    public required IReadOnlySet<RuleViolation> Baseline { get; init; }
}
