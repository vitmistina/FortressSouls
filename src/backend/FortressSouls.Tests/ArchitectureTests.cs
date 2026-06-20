namespace FortressSouls.Tests;

using FortressSouls.Application;
using FortressSouls.Domain;

/// <summary>
/// Architecture tests ensuring modular-monolith boundaries are respected.
/// </summary>
public class ArchitectureTests
{
    /// <summary>
    /// Verify that Domain project has no inappropriate external dependencies.
    /// Domain should only reference System namespaces, not adapters or framework.
    /// </summary>
    [Fact]
    public void DomainProjectHasNoDependenciesOnAdaptersOrFramework()
    {
        var domainAssembly = typeof(SentinelType).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        var forbidden = referencedAssemblies
            .Where(a => (a.Name ?? "").StartsWith("FortressSouls.")
                || (a.Name ?? "").StartsWith("Microsoft.AspNetCore")
                || (a.Name ?? "").StartsWith("Microsoft.EntityFrameworkCore"))
            .ToList();

        Assert.Empty(forbidden);
    }

    /// <summary>
    /// Verify that the solution structure exists as expected.
    /// </summary>
    [Fact]
    public void SolutionStructureIsValid()
    {
        // These assemblies should be loadable and present in the test context.
        var domainAssembly = typeof(SentinelType).Assembly;
        var appAssembly = typeof(HealthResponse).Assembly;

        Assert.NotNull(domainAssembly);
        Assert.NotNull(appAssembly);
        Assert.Equal("FortressSouls.Domain", domainAssembly.GetName().Name);
        Assert.Equal("FortressSouls.Application", appAssembly.GetName().Name);
    }
}


