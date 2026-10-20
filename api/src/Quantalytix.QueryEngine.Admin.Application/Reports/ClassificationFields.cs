using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Which bindings of a dataset version a classification can start from.</summary>
public static class ClassificationFields
{
    /// <summary>The binding kinds that classify a field's values into members, which a bridge can then remap.</summary>
    private static readonly string[] MemberKinds = [BindingKinds.Direct, BindingKinds.Mapped, BindingKinds.Range];

    /// <summary>Whether a binding classifies its field's values into members, grouped by member, as a bridge classification needs.</summary>
    /// <param name="binding">A dimension binding.</param>
    /// <returns>True when a bridge can start from the binding's members.</returns>
    public static bool GroupsMembers(DimensionBindingRecord binding)
    {
        return MemberKinds.Contains(binding.BindingKind) && binding.GroupingMode == GroupingModes.Member;
    }

    /// <summary>
    /// Whether a <c>binding</c> route can reuse a field's binding: an enabled binding that groups members of the calculation
    /// dimension's dimension, which the route regroups as the calculation dimension says.
    /// </summary>
    /// <param name="dataset">The dataset version a release pins.</param>
    /// <param name="fieldKey">The route's field.</param>
    /// <param name="dimensionId">The calculation dimension's dimension.</param>
    /// <returns>True when the route can reuse the field's binding.</returns>
    public static bool RoutesThrough(PinnedDatasetVersion dataset, string fieldKey, long dimensionId)
    {
        return dataset.BindingOf(fieldKey) is { IsEnabled: true } binding && GroupsMembers(binding) && binding.DimensionId == dimensionId;
    }

    /// <summary>Whether a bridge classification can read a field: its binding groups members of the bridge's source dimension.</summary>
    /// <param name="dataset">The dataset version a release pins.</param>
    /// <param name="fieldKey">The classification's field.</param>
    /// <param name="bridge">The classification's bridge.</param>
    /// <returns>True when the field's binding feeds the bridge.</returns>
    public static bool BridgesFrom(PinnedDatasetVersion dataset, string fieldKey, BridgeRecord bridge)
    {
        return dataset.BindingOf(fieldKey) is { } binding && GroupsMembers(binding) && binding.DimensionId == bridge.SourceDimensionId;
    }
}
