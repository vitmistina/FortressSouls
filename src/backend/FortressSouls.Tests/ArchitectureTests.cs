namespace FortressSouls.Tests;

using FortressSouls.Application;
using FortressSouls.Domain;

public class ArchitectureTests
{
    [Fact]
    public void DomainProjectHasNoDependenciesOnAdaptersOrFramework()
    {
        var domainAssembly = typeof(SentinelType).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        var forbidden = referencedAssemblies
            .Where(a => (a.Name ?? "").StartsWith("FortressSouls.", StringComparison.Ordinal)
                || (a.Name ?? "").StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || (a.Name ?? "").StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void DwarfFortressPortExposesOnlyListAndByIdSnapshotOperations()
    {
        var methods = typeof(IDwarfFortressAdapter)
            .GetMethods()
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Collection(
            methods,
            method =>
            {
                Assert.Equal(nameof(IDwarfFortressAdapter.GetDwarfSnapshotAsync), method.Name);
                var parameters = method.GetParameters();
                Assert.Collection(
                    parameters,
                    parameter => Assert.Equal(typeof(DwarfId), parameter.ParameterType),
                    parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
            },
            method =>
            {
                Assert.Equal(nameof(IDwarfFortressAdapter.ListDwarvesAsync), method.Name);
                var parameters = method.GetParameters();
                Assert.Collection(
                    parameters,
                    parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
            });
    }

    [Fact]
    public void SolutionAssembliesRemainLoadable()
    {
        var domainAssembly = typeof(SentinelType).Assembly;
        var applicationAssembly = typeof(HealthResponse).Assembly;

        Assert.Equal("FortressSouls.Domain", domainAssembly.GetName().Name);
        Assert.Equal("FortressSouls.Application", applicationAssembly.GetName().Name);
    }
}
