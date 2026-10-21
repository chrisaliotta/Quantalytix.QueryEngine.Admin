namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>Every variable a report source supplies, as <c>PUT reports/{reportId}/sources/{sourceId}/variables</c> takes them.</summary>
public sealed record SourceVariablesInput
{
    /// <summary>The source's complete variable list, each variable once; a variable the list leaves out leaves the profiles' picks too.</summary>
    public required IReadOnlyList<SourceVariableInput> Variables { get; init; }
}
