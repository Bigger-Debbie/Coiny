using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Coiny.API.Services.Recovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Coiny.API.Controllers;

[ApiController, Authorize, Route("api/household-data"), HouseholdStorage]
[EnableRateLimiting("household-recovery")]
[RequestSizeLimit(BackupEncryption.MaximumFile + 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = BackupEncryption.MaximumFile + 1024 * 1024)]
public sealed class HouseholdRecoveryController(IServiceProvider services, IConfiguration configuration, IControlContext control, ICurrentUserService current) : ControllerBase
{
    public sealed record PasswordRequest(string CurrentPassword);
    private static readonly SemaphoreSlim Operations = new(1, 1);
    private HouseholdRecoveryService Service => configuration.GetValue<bool>("HybridStorage:Enabled") ? services.GetRequiredService<HouseholdRecoveryService>() : throw new ArgumentException("Household backup tools are not available on this installation yet.");
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities() {
        Response.Headers.CacheControl = "no-store";
        if (!configuration.GetValue<bool>("HybridStorage:Enabled")) return Ok(new { enabled = false, owner = false, maximumFileBytes = BackupEncryption.MaximumFile });
        var id = await current.GetCurrentHouseholdIdAsync();
        return Ok(new { enabled = configuration.GetValue<bool>("HybridStorage:Enabled"), owner = await control.HouseholdMembers.AnyAsync(x => x.HouseholdId == id && x.UserId == current.GetUserId() && x.Role == HouseholdRole.Owner), maximumFileBytes = BackupEncryption.MaximumFile });
    }
    private async Task<IActionResult> Run(Func<Task<IActionResult>> work) {
        Response.Headers.CacheControl = "no-store";
        if (!await Operations.WaitAsync(TimeSpan.FromSeconds(5))) return StatusCode(429, new { message = "Another data operation is running. Please try again shortly." });
        try { return await work(); }
        catch (UnauthorizedAccessException) { return StatusCode(403, new { message = "Only a household owner can use these data tools." }); }
        catch (BudgetConflictException error) { return Conflict(new { message = error.Message }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (System.Security.Cryptography.CryptographicException) { return BadRequest(new { message = "The preview is no longer valid. Preview the backup again." }); }
        catch (SqliteException) { return BadRequest(new { message = "The backup database could not be validated. No data was replaced." }); }
        finally { Operations.Release(); }
    }
    [HttpPost("backup")]
    public Task<IActionResult> Backup(CreateBackupRequest input) => Run(async () => File(await Service.CreateAsync(input), "application/octet-stream", "coiny-household-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".coiny"));
    [HttpPost("export")]
    public Task<IActionResult> Export(PasswordRequest input) => Run(async () => File(await Service.ExportAsync(input.CurrentPassword ?? ""), "application/zip", "coiny-household-export.zip"));
    [HttpPost("restore/preview")]
    public Task<IActionResult> Preview([FromForm] IFormFile file, [FromForm] string currentPassword, [FromForm] string? backupPassword) => Run(async () => {
        if (file.Length > BackupEncryption.MaximumFile || file.Length == 0) throw new ArgumentException("Choose a Coiny backup smaller than 64 MiB.");
        using var bytes = new MemoryStream(); await file.CopyToAsync(bytes);
        return Ok(await Service.PreviewAsync(bytes.ToArray(), backupPassword, currentPassword ?? ""));
    });
    [HttpPost("restore/confirm")]
    public Task<IActionResult> Restore(RestoreRequest input) => Run(async () => Ok(new { safetyBackupId = await Service.RestoreAsync(input) }));
    [HttpGet("safety")]
    public Task<IActionResult> Safety() => Run(async () => Ok(await Service.SafetyListAsync()));
    [HttpPost("safety/{id:guid}")]
    public Task<IActionResult> DownloadSafety(Guid id, PasswordRequest input) => Run(async () => File(await Service.SafetyAsync(id, input.CurrentPassword ?? ""), "application/octet-stream", "coiny-before-restore.coiny"));
}
