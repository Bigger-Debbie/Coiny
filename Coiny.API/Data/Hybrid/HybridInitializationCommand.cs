using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Data.Hybrid;

// Explicit offline bootstrap for a new installation. Never an upgrade/reset path.
internal static class HybridInitializationCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        try {
            if (args is not ["--initialize-hybrid", "--offline"]) throw new ArgumentException("Explicit offline initialization is required.");
            var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
            builder.Services.AddHybridStorageFoundation(builder.Configuration);
            using var host = builder.Build();
            var root = host.Services.GetRequiredService<HybridStorageRoot>();
            if (Directory.EnumerateFileSystemEntries(root.DirectoryPath).Any(x => Path.GetFileName(x) != ".coiny-owner.lock"))
                throw new InvalidOperationException("Household storage must be empty for initialization.");
            await using var scope = host.Services.CreateAsyncScope();
            var control = scope.ServiceProvider.GetRequiredService<ControlContext>();
            var tables = await control.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_schema='public'").SingleAsync();
            if (tables != 0) throw new InvalidOperationException("The control database must be empty. Initialization never resets or upgrades existing stores.");
            await control.Database.MigrateAsync();
            Console.WriteLine("Initialized an empty hybrid installation. No users, households or demo financial data were created.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine($"Hybrid initialization refused/failed ({error.GetType().Name}). Use empty offline stores; existing data was not reset.");
            return 1;
        }
    }
}
