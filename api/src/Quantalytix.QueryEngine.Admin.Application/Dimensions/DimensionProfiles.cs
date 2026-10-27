using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The code registry of system dimension profiles (OI-16). <c>branch</c> and <c>calendar</c> match the seeded system dimensions
/// (Branch keeps role <c>branch</c> and member <c>BR01</c>; Calendar is a time dimension without roles); the others are the types
/// the dimension manager offers to define.
/// </summary>
public static class DimensionProfiles
{
    /// <summary>A profile the dimension manager lists and offers to define.</summary>
    public const string ManagerVisibility = "manager";

    /// <summary>A profile whose dimension exactly one exists per tenant.</summary>
    public const string SingleCardinality = "one";

    /// <summary>Every profile, in the order the manager lists them.</summary>
    public static readonly IReadOnlyList<DimensionProfile> All =
    [
        new()
        {
            Key = "branch",
            DisplayName = "Branch",
            Kind = DimensionKinds.Hierarchical,
            Owner = "Core",
            EditPolicy = DimensionEditPolicies.Extend,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = ["branch"],
            RequiredMemberKeys = ["BR01"],
            Cardinality = SingleCardinality,
            MemberPropertySchema =
            [
                new() { Key = "photoUrl", Label = "Photo", Type = DimensionPropertyTypes.Image, Description = "Shown on branch scorecards." },
                new() { Key = "address", Label = "Address", Type = DimensionPropertyTypes.Text, Required = true },
                new() { Key = "geo", Label = "Location", Type = DimensionPropertyTypes.Geo, Description = "Latitude and longitude of the branch entrance." },
                new() { Key = "openedOn", Label = "Opened on", Type = DimensionPropertyTypes.Date },
                new()
                {
                    Key = "catchment",
                    Label = "Catchment boundary",
                    Type = DimensionPropertyTypes.Text,
                    Format = DimensionPropertyTypes.GeoJsonFormat,
                    Description = "GeoJSON polygon for the branch catchment area."
                }
            ]
        },
        new()
        {
            Key = "calendar",
            DisplayName = "Calendar",
            Kind = DimensionKinds.Time,
            Owner = "Platform",
            EditPolicy = DimensionEditPolicies.Locked,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = [],
            RequiredMemberKeys = [],
            Cardinality = SingleCardinality,
            MemberPropertySchema = []
        },
        new()
        {
            Key = "officer",
            DisplayName = "Officer",
            Kind = DimensionKinds.Categorical,
            Owner = "Core",
            EditPolicy = DimensionEditPolicies.Extend,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = ["officer"],
            RequiredMemberKeys = [],
            Cardinality = SingleCardinality,
            MemberPropertySchema =
            [
                new() { Key = "email", Label = "Email", Type = DimensionPropertyTypes.Text, Required = true },
                new() { Key = "photoUrl", Label = "Photo", Type = DimensionPropertyTypes.Image },
                new() { Key = "title", Label = "Title", Type = DimensionPropertyTypes.Text }
            ]
        },
        new()
        {
            Key = "product",
            DisplayName = "Product",
            Kind = DimensionKinds.Hierarchical,
            Owner = "Profitability",
            EditPolicy = DimensionEditPolicies.Locked,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = ["product"],
            RequiredMemberKeys = ["MORTGAGE", "AUTO", "COMMERCIAL", "DEPOSITS"],
            Cardinality = SingleCardinality,
            MemberPropertySchema =
            [
                new() { Key = "glPrefix", Label = "GL prefix", Type = DimensionPropertyTypes.Text },
                new() { Key = "ftpCurve", Label = "FTP curve", Type = DimensionPropertyTypes.Select, Options = ["SOFR", "Treasury", "Prime"] }
            ]
        },
        new()
        {
            Key = "planVersion",
            DisplayName = "Scenario",
            Kind = DimensionKinds.Hierarchical,
            Owner = "Planning",
            EditPolicy = DimensionEditPolicies.Extend,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = ["planVersion"],
            RequiredMemberKeys = [],
            Cardinality = SingleCardinality,
            SingleRole = true,
            MemberPropertySchema =
            [
                new() { Key = "kind", Label = "Kind", Type = DimensionPropertyTypes.Select, Options = ["budget", "forecast"], Required = true },
                new() { Key = "horizonStart", Label = "Horizon start", Type = DimensionPropertyTypes.Date },
                new() { Key = "horizonEnd", Label = "Horizon end", Type = DimensionPropertyTypes.Date },
                new() { Key = "asOf", Label = "As of", Type = DimensionPropertyTypes.Date },
                new() { Key = "status", Label = "Status", Type = DimensionPropertyTypes.Select, Options = ["working", "submitted", "approved", "locked"] }
            ]
        },
        new()
        {
            Key = "channel",
            DisplayName = "Channel",
            Kind = DimensionKinds.Categorical,
            Owner = "Digital",
            EditPolicy = DimensionEditPolicies.Open,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = ["channel"],
            RequiredMemberKeys = ["BRANCH", "ONLINE", "MOBILE"],
            Cardinality = SingleCardinality,
            SeedNames = new Dictionary<string, string> { ["channel"] = "Channel", ["BRANCH"] = "Branch", ["ONLINE"] = "Online banking", ["MOBILE"] = "Mobile app" },
            MemberPropertySchema =
            [
                new() { Key = "isDigital", Label = "Digital channel", Type = DimensionPropertyTypes.Boolean },
                new() { Key = "helpUrl", Label = "Help page", Type = DimensionPropertyTypes.Url }
            ]
        },
        new()
        {
            Key = "costCentre",
            DisplayName = "Cost centre",
            Kind = DimensionKinds.Hierarchical,
            Owner = "Cost Allocation",
            EditPolicy = DimensionEditPolicies.Extend,
            Visibility = ManagerVisibility,
            RequiredRoleKeys = ["costCentre"],
            RequiredMemberKeys = ["UNALLOCATED"],
            Cardinality = SingleCardinality,
            SeedNames = new Dictionary<string, string> { ["costCentre"] = "Cost centre", ["UNALLOCATED"] = "Unallocated" },
            MemberPropertySchema =
            [
                new() { Key = "manager", Label = "Manager", Type = DimensionPropertyTypes.Text },
                new() { Key = "headcount", Label = "Headcount", Type = DimensionPropertyTypes.Number }
            ]
        },
        new()
        {
            Key = "glAccountClass",
            DisplayName = "GL account class",
            Kind = DimensionKinds.Categorical,
            Owner = "General Ledger",
            EditPolicy = DimensionEditPolicies.Extend,
            Visibility = "module",
            RequiredRoleKeys = ["glAccountClass"],
            RequiredMemberKeys = ["ASSET", "LIABILITY", "EQUITY", "INCOME", "EXPENSE"],
            Cardinality = SingleCardinality,
            MemberPropertySchema =
            [
                new() { Key = "normalBalance", Label = "Normal balance", Type = DimensionPropertyTypes.Select, Options = ["debit", "credit"] }
            ]
        },
        new()
        {
            Key = "systemAccountClass",
            DisplayName = "System account class",
            Kind = DimensionKinds.Categorical,
            Owner = "Platform",
            EditPolicy = DimensionEditPolicies.Locked,
            Visibility = "hidden",
            RequiredRoleKeys = [],
            RequiredMemberKeys = [],
            Cardinality = SingleCardinality,
            MemberPropertySchema = []
        }
    ];

    /// <summary>The profiles by key.</summary>
    private static readonly FrozenDictionary<string, DimensionProfile> ByKey = All.ToFrozenDictionary(profile => profile.Key, StringComparer.Ordinal);

    /// <summary>The profile with a key, or null when the registry has none.</summary>
    /// <param name="key">A profile key.</param>
    /// <returns>The profile, or null.</returns>
    public static DimensionProfile? Find(string key)
    {
        return ByKey.GetValueOrDefault(key);
    }

    /// <summary>The profile a dimension follows: its <c>dimension_profile_key</c>'s, or null for a user or report dimension.</summary>
    /// <param name="profileKey">The dimension's profile key.</param>
    /// <returns>The profile, or null.</returns>
    public static DimensionProfile? Of(string? profileKey)
    {
        return profileKey is null ? null : ByKey[profileKey];
    }

    /// <summary>Whether the dimension manager lists and offers a profile: manager-visible and single-instance.</summary>
    /// <param name="profile">A profile.</param>
    /// <returns>True for the profiles the manager shows.</returns>
    public static bool IsOffered(DimensionProfile profile)
    {
        return profile.Visibility == ManagerVisibility && profile.Cardinality == SingleCardinality;
    }
}
