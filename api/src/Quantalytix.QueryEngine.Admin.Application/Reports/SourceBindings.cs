using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Which bindings of a dataset version a bridge source can start from.</summary>
public static class SourceBindings
{
    /// <summary>The binding kinds that classify a field's values into members, which a bridge can then remap.</summary>
    private static readonly string[] MemberKinds = [BindingKinds.Direct, BindingKinds.Mapped, BindingKinds.Range];

    /// <summary>Whether a binding feeds a bridge: it is enabled and classifies its field's values into members of its role's template.</summary>
    /// <param name="binding">A dimension binding.</param>
    /// <returns>True when a bridge can start from the binding's members.</returns>
    public static bool FeedsBridges(DimensionBindingRecord binding)
    {
        return binding.IsEnabled && MemberKinds.Contains(binding.BindingKind);
    }

    /// <summary>Whether a bridge source can start from a role: the dataset version has a binding of it that feeds bridges.</summary>
    /// <param name="dataset">The dataset version a release pins.</param>
    /// <param name="roleId">The source's role.</param>
    /// <returns>True when the role's binding feeds the source's bridge.</returns>
    public static bool BridgesFrom(PinnedDatasetVersion dataset, long roleId)
    {
        return dataset.Bindings.Any(binding => binding.RoleId == roleId && FeedsBridges(binding));
    }
}
