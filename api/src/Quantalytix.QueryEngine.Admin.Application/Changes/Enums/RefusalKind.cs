namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>Why a change or command was refused; each kind is one HTTP status of the API.</summary>
public enum RefusalKind
{
    /// <summary>An input is invalid (400): <see cref="Refusal.Errors"/> says where.</summary>
    Invalid,

    /// <summary>A precondition failed (409): <see cref="Refusal.Detail"/> says which.</summary>
    Conflict,

    /// <summary>The result would break rules (422): <see cref="Refusal.Violations"/> lists them.</summary>
    RulesViolated
}
