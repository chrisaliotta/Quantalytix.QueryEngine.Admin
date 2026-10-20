namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_variable_source</c> row: one dataset measure that can supply a variable, named by key in the dataset version a release pins.</summary>
public sealed record VariableSourceRecord
{
    /// <summary>Primary key.</summary>
    public required long VariableSourceId { get; init; }

    /// <summary>The owning model version.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>The variable the source supplies.</summary>
    public required long VariableId { get; init; }

    /// <summary>Key, unique among the version's sources.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The dataset; the release decides its version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The field whose binding classifies rows into the report dimension.</summary>
    public required string ReportFieldKey { get; init; }

    /// <summary>The time field the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }

    /// <summary>The dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Optional structured filter over the dataset's fields.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>Whether profiles may pick the source.</summary>
    public required bool IsEnabled { get; init; }
}
