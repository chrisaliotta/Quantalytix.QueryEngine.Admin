namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A variable source of a draft model version, as its create and edit endpoints take it.</summary>
public sealed record VariableSourceInput
{
    /// <summary>The variable of the version that the source supplies.</summary>
    public required long VariableId { get; init; }

    /// <summary>Key, unique among the version's sources.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The dataset; the release that pins the report decides its version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The dataset field whose binding classifies rows into the report dimension.</summary>
    public required string ReportFieldKey { get; init; }

    /// <summary>The dataset time field the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }

    /// <summary>The dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Optional structured filter over the dataset's fields, in the query contract's <c>filterNode</c> vocabulary.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>Whether profiles may pick the source.</summary>
    public required bool IsEnabled { get; init; }
}
