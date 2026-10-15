namespace Quantalytix.QueryEngine.Admin.Api.Errors;

/// <summary>The 409 problem a delete answers when other rows still reference what it would delete; its detail names them.</summary>
internal static class DeleteBlockedProblem
{
    /// <summary>The problem's title.</summary>
    public const string Title = "Delete blocked";
}
