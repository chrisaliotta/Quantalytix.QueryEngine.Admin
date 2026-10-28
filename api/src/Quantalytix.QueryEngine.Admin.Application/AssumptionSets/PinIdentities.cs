using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// The tenant's dimensions, mapping sets and bridges with their live and frozen versions, indexed for the pin read models (OI-38):
/// a pin's state, its identity's published version, and whether a report owns the identity, which captures leave out (R9).
/// </summary>
/// <param name="all">Every identity, by kind and key.</param>
public sealed class PinIdentities(IReadOnlyList<PinIdentity> all)
{
    /// <summary>The identities by kind and id.</summary>
    private readonly Dictionary<(string Kind, long Id), PinIdentity> _byId = all.ToDictionary(identity => (identity.Kind, identity.Id));

    /// <summary>Every identity, by kind and key.</summary>
    public IReadOnlyList<PinIdentity> All => all;

    /// <summary>One identity.</summary>
    /// <param name="kind">A pin kind.</param>
    /// <param name="identityId">The identity.</param>
    /// <returns>The identity.</returns>
    public PinIdentity Of(string kind, long identityId)
    {
        return _byId[(kind, identityId)];
    }

    /// <summary>Whether a pin's identity belongs to a report.</summary>
    /// <param name="pin">A pin.</param>
    /// <returns>True for a report dimension or a bridge into one.</returns>
    public bool IsReportOwned(PinnedVersion pin)
    {
        return Of(PinKinds.Of(pin.Kind), pin.IdentityId).ReportId is not null;
    }

    /// <summary>A pin's row: its identity's published version and its state.</summary>
    /// <param name="pin">A pin.</param>
    /// <returns>The row.</returns>
    public PinRow Row(PinnedVersion pin)
    {
        var kind = PinKinds.Of(pin.Kind);
        var identity = Of(kind, pin.IdentityId);
        return new PinRow
        {
            Kind = kind,
            IdentityId = pin.IdentityId,
            VersionId = pin.VersionId,
            PublishedVersionId = identity.Versions.SingleOrDefault(version => version.Status == PublicationStatuses.Published)?.Id,
            State = identity.ReportId is not null ? PinStates.ReportOwned
                : identity.Versions.Single(version => version.Id == pin.VersionId).Status == PublicationStatuses.Published ? PinStates.Live
                : PinStates.Frozen
        };
    }

    /// <summary>The rows of some pins: dimensions, mapping sets, then bridges, each by identity key.</summary>
    /// <param name="pins">The pins.</param>
    /// <returns>The rows.</returns>
    public IReadOnlyList<PinRow> Rows(IEnumerable<PinnedVersion> pins)
    {
        return
        [
            .. pins.Select(Row)
                .OrderBy(row => PinKinds.Order(row.Kind))
                .ThenBy(row => Of(row.Kind, row.IdentityId).Key, StringComparer.Ordinal)
        ];
    }
}
