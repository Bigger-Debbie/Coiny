namespace Coiny.API.Data.Hybrid;

internal static class MaintenanceCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        try {
            var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
            builder.Services.AddHybridStorageFoundation(builder.Configuration);
            using var host = builder.Build(); // Operator only: no HTTP listener or background jobs.
            var maintenance = host.Services.GetRequiredService<InstallationMaintenance>();
            if (args is ["--maintenance", "status"]) {
                var operation = await maintenance.StatusAsync();
                Console.WriteLine(operation is Guid id ? $"Paused/requested: {id}. Stop API/jobs before backup or restore." : "Running.");
                return 0;
            }
            if (args.Length != 3 || !Guid.TryParse(args[2], out var token) || token == Guid.Empty)
                throw new ArgumentException("A maintenance action and nonempty operation UUID are required.");
            if (args[1] == "pause") {
                await maintenance.RequestAsync(token);
                await using var drain = await maintenance.DrainAsync(token);
                Console.WriteLine("Maintenance requested and participating work drained. Stop API/jobs before taking coordinated snapshots. Admission stays paused until explicit resume.");
                return 0;
            }
            if (args[1] == "resume") {
                await maintenance.ResumeAsync(token);
                Console.WriteLine("Maintenance cleared. API/jobs may accept new work.");
                return 0;
            }
            throw new ArgumentException("Unknown maintenance action.");
        } catch (Exception error) {
            Console.Error.WriteLine($"Maintenance command failed ({error.GetType().Name}). Check status; a requested pause is retained after failure. No automatic resume was performed.");
            return 1;
        }
    }
}
