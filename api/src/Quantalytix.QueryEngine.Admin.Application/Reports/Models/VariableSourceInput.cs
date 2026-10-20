namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A variable source of an editable report version, as its create and edit endpoints take it.</summary>
public sealed record VariableSourceInput
{
    /// <summary>The key of the model variable the source supplies; the model's published version must have it at publication.</summary>
    public required string VariableKey { get; init; }

    /// <summary>Key, unique among the version's sources.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The dataset; the release the report runs against decides its version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>How the dataset's rows classify into the report's dimension.</summary>
    public required ClassificationInput Classification { get; init; }

    /// <summary>The dataset time field the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }

    /// <summary>The dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Optional structured filter over the dataset's fields, in the query contract's <c>filterNode</c> vocabulary.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>Whether profiles may pick the source.</summary>
    public required bool IsEnabled { get; init; }
}
