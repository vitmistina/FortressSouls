namespace FortressSouls.DwarfFortress;

using FortressSouls.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DwarfFortressServiceCollectionExtensions
{
    public static IServiceCollection AddFortressSoulsDwarfFortress(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration
            .GetSection(DfHackProcessAdapterOptions.ConfigurationSectionPath)
            .Get<DfHackProcessAdapterOptions>() ?? new DfHackProcessAdapterOptions();

        options.Validate();
        services.AddSingleton(options);
        services.AddSingleton(new DfHackAdapterStatusTracker(options.Enabled));

        if (!options.Enabled)
        {
            services.AddSingleton<IDwarfFortressAdapter, FakeDwarfFortressAdapter>();
            services.AddSingleton(new DwarfAdapterDescriptor("Fake"));
            services.AddSingleton<IDwarfAdapterStatusReader>(new StaticDwarfAdapterStatusReader("Fake"));
            return services;
        }

        services.AddSingleton<IDfHackTcpPreflight, TcpDfHackPreflight>();
        services.AddSingleton<IDfHackProcessRunner, DfHackProcessRunner>();
        services.AddSingleton<IDfHackAdapterStatusRecorder>(sp => sp.GetRequiredService<DfHackAdapterStatusTracker>());
        services.AddSingleton<IDwarfAdapterStatusReader>(sp => sp.GetRequiredService<DfHackAdapterStatusTracker>());
        services.AddSingleton<IDwarfFortressAdapter, DfHackDwarfFortressAdapter>();
        services.AddSingleton(new DwarfAdapterDescriptor("DfHackProcess"));
        return services;
    }
}
