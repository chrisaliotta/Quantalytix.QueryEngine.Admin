using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>
/// Confirms the <see cref="DeleteCheck"/> that <see cref="DeleteBlockers"/> builds (blueprint 01, Delete checks): allowed with no
/// message and no reasons when nothing blocks; otherwise one coded reason per blocker, in the order found, and the one-sentence
/// message that names them. <c>GET .../delete-check</c> and the 409 of <c>DELETE</c> both answer from this check, so they agree.
/// </summary>
public sealed class DeleteBlockersTests
{
    /// <summary>Nothing added: the delete is allowed and the check carries no message.</summary>
    [Fact]
    public void NoBlockers_AllowTheDelete()
    {
        var check = new DeleteBlockers("Member 'CA'").Check();

        Assert.True(check.Allowed);
        Assert.Null(check.Message);
        Assert.Empty(check.Reasons);
    }

    /// <summary>A named reason with no names adds nothing, so an empty reference list never blocks.</summary>
    [Fact]
    public void NamedReasonWithoutNames_AddsNothing()
    {
        var blockers = new DeleteBlockers("Member 'CA'");
        blockers.AddNamed(DeleteBlockerCodes.HasChildren, "it has child members", []);

        Assert.True(blockers.Check().Allowed);
    }

    /// <summary>Each blocker becomes a coded reason; names are distinct, ordered and capped; the message joins every reason.</summary>
    [Fact]
    public void Blockers_BecomeCodedReasonsAndOneMessage()
    {
        var blockers = new DeleteBlockers("Dimension 'Purpose'");
        blockers.Add(DeleteBlockerCodes.NotRemovable, "it is not removable");
        blockers.AddNamed(DeleteBlockerCodes.ReferencedByBridge, "referenced by bridges", [.. Enumerable.Range(1, 12).Select(i => $"'b{i:D2}'"), "'b01'"]);

        var check = blockers.Check();

        Assert.False(check.Allowed);
        Assert.Equal([DeleteBlockerCodes.NotRemovable, DeleteBlockerCodes.ReferencedByBridge], check.Reasons.Select(reason => reason.Code));
        Assert.Equal("referenced by bridges 'b01', 'b02', 'b03', 'b04', 'b05', 'b06', 'b07', 'b08', 'b09', 'b10' and 2 more", check.Reasons[1].Message);
        Assert.Equal($"Dimension 'Purpose' cannot be deleted: it is not removable; {check.Reasons[1].Message}.", check.Message);
    }
}
