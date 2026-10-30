using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coiny.API.Data.Hybrid;

public sealed class HybridStorageOptions
{
    public string ControlConnection { get; set; } = "";
    public string DataRoot { get; set; } = "";
    public string Node { get; set; } = "";
}

public static class HybridStorageRegistration
{
    // Explicit opt-in through HybridStorage configuration; legacy remains the default.
    public static IServiceCollection AddHybridStorageFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HybridStorageOptions>().Bind(configuration.GetSection("HybridStorage"))
            .Validate(x => !string.IsNullOrWhiteSpace(x.ControlConnection), "A control connection is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.DataRoot) && Path.IsPathFullyQualified(x.DataRoot), "An absolute household storage directory is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Node) && x.Node.Length <= 100, "A valid storage node is required.")
            .ValidateOnStart();
        services.AddDbContextFactory<ControlContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<HybridStorageOptions>>().Value.ControlConnection));
        services.AddSingleton(provider => {
            var settings = provider.GetRequiredService<IOptions<HybridStorageOptions>>().Value;
            return new HybridStorageRoot(settings.DataRoot, settings.Node);
        });
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HouseholdDatabaseService>();
        services.AddSingleton<InstallationMaintenance>();
        return services;
    }
}
