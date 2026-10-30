using Coiny.API.Extensions;
using Coiny.API.Configuration;
using System.Text.Json.Serialization;
using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Microsoft.EntityFrameworkCore;

if (args.FirstOrDefault() == "--initialize-hybrid") {
    Environment.ExitCode = await HybridInitializationCommand.RunAsync(args);
    return;
}

if (args.FirstOrDefault() is "--verify-household" or "--household-backups" or "--restore-household-backups" or "--recovery-security") {
    Environment.ExitCode = await PersonalRehearsalCommand.RunAsync(args);
    return;
}

if (args.FirstOrDefault() == "--maintenance") {
    Environment.ExitCode = await MaintenanceCommand.RunAsync(args);
    return;
}

if (args.FirstOrDefault() is "--migration-plan" or "--migrate-snapshot") {
    Environment.ExitCode = await LegacyMigrationCommand.RunAsync(args);
    return;
}

var seedDemo = args.Contains("--seed-demo");
var migrateDockerTest = args.Contains("--migrate-docker-test");
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--seed-demo" && argument != "--migrate-docker-test").ToArray());

builder.Services.AddDatabase(builder.Configuration);
var hybrid = builder.Configuration.GetValue<bool>("HybridStorage:Enabled");
builder.Services.AddIdentityServices(hybrid);

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationServices(hybrid, builder.Configuration.GetValue<bool>("Operations:ScheduledJobsEnabled", true));
builder.Services.AddHouseholdInvitationRateLimits();
builder.Services.AddAccountSecurityRateLimits();
builder.Services.Configure<RecoveryEmailOptions>(builder.Configuration.GetSection("RecoveryEmail"));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDevClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (migrateDockerTest) {
    if (!hybrid) throw new InvalidOperationException("Hybrid storage must be explicitly enabled for migration.");
    try { await LegacyTestMigration.RunAsync(app.Services, app.Configuration, app.Environment); }
    catch (Exception error) {
        // CLI failures should exit cleanly, not leave an abort/core-dump process
        // holding the household ownership lock. Never print financial values.
        Console.Error.WriteLine($"Docker test migration failed ({error.GetType().Name}): {error.Message}");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
if (hybrid) {
    await using var startup = app.Services.CreateAsyncScope();
    var control = startup.ServiceProvider.GetRequiredService<ControlContext>();
    if ((await control.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Apply control migrations before starting the hybrid API.");
    if (await control.Set<LegacyImportState>().AnyAsync(x => x.CompletedAt == null)) throw new InvalidOperationException("Complete the interrupted test migration before starting the API.");
    // A paused installation may start to serve the maintenance response. It must
    // not repair/provision storage until admission reopens.
    try {
        await using var admission = await startup.ServiceProvider.GetRequiredService<InstallationMaintenance>().EnterAsync();
        await startup.ServiceProvider.GetRequiredService<HouseholdDatabaseService>().RecoverProvisioningAsync();
    } catch (InstallationMaintenanceException) { }
}
if (seedDemo)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Demo seeding is only available in Development.");

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngularDevClient");

app.UseRouting();
if (hybrid) { app.UseHybridFailureResponses(); app.UseInstallationMaintenance(); }
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (hybrid) app.UseHybridStorage();

app.MapControllers();

app.Run();
