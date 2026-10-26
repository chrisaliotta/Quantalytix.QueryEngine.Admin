using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Quantalytix.QueryEngine.Admin.WebApi.Development;

/// <summary>
/// Removes every controller in the Development feature namespace. Registered outside the Development environment, so those
/// endpoints are never routed there.
/// </summary>
public sealed class DevelopmentControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    /// <summary>The namespace of the Development feature.</summary>
    private static readonly string DevelopmentNamespace = typeof(DevelopmentControllerFeatureProvider).Namespace!;

    /// <summary>Removes the Development controllers that the default provider discovered.</summary>
    /// <param name="parts">The application parts.</param>
    /// <param name="feature">The discovered controllers.</param>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(controller => controller.Namespace == DevelopmentNamespace).ToList())
        {
            feature.Controllers.Remove(controller);
        }
    }
}
