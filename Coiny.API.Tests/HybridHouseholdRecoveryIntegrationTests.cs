using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class HybridHouseholdRecoveryIntegrationTests
{
    private static async Task<byte[]> Backup(HybridApiIntegrationTests.Fixture site, bool portable = false)
    {
        var response = await site.Client.PostAsJsonAsync("/api/household-data/backup", new { currentPassword = "Synthetic!2026", passwordProtected = portable, backupPassword = portable ? "Portable backup passphrase" : null });
        response.EnsureSuccessStatusCode(); return await response.Content.ReadAsByteArrayAsync();
    }
    private static async Task<HttpResponseMessage> Preview(HybridApiIntegrationTests.Fixture site, byte[] file, bool portable = false)
    {
        using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(file), "file", "backup.coiny");
        form.Add(new StringContent("Synthetic!2026"), "currentPassword"); if (portable) form.Add(new StringContent("Portable backup passphrase"), "backupPassword");
        return await site.Client.PostAsync("/api/household-data/restore/preview", form);
    }
    private static async Task Setup(HybridApiIntegrationTests.Fixture site, string name, bool offsetHousehold = false)
    {
        await site.InitializeAsync(); if (offsetHousehold) await site.Storage.HouseholdAsync("separate-unrelated-owner"); site.Use(await site.Register(name));
        (await site.Client.PostAsJsonAsync("/api/households", new { name })).EnsureSuccessStatusCode();
        (await site.Client.PostAsJsonAsync("/api/accounts", new { name = "Original checking", accountType = "Checking", openingBalance = 1000, openingBalanceDate = "2026-01-01" })).EnsureSuccessStatusCode();
    }
    [PostgresFact] public async Task Linked_restore_previews_detect_changes_and_safety_backup_preserves_replaced_data()
    {
        await using var site = new HybridApiIntegrationTests.Fixture(); await Setup(site, "backup-owner");
        var file = await Backup(site);
        var preview = await Preview(site, file); preview.EnsureSuccessStatusCode();
        var id = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("previewId").GetGuid();
        (await site.Client.PostAsJsonAsync("/api/accounts", new { name = "New checking", accountType = "Checking", openingBalance = 20, openingBalanceDate = "2026-01-01" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await site.Client.PostAsJsonAsync("/api/household-data/restore/confirm", new { previewId = id, currentPassword = "Synthetic!2026", confirmReplacement = true })).StatusCode);
        var fresh = await Preview(site, file); fresh.EnsureSuccessStatusCode();
        id = (await fresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("previewId").GetGuid();
        var restored = await site.Client.PostAsJsonAsync("/api/household-data/restore/confirm", new { previewId = id, currentPassword = "Synthetic!2026", confirmReplacement = true }); Assert.True(restored.IsSuccessStatusCode, await restored.Content.ReadAsStringAsync());
        Assert.Single((await site.Client.GetFromJsonAsync<JsonElement>("/api/accounts")).EnumerateArray());
        var safetyId = (await restored.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("safetyBackupId").GetGuid();
        var safety = await site.Client.PostAsJsonAsync($"/api/household-data/safety/{safetyId}", new { currentPassword = "Synthetic!2026" }); safety.EnsureSuccessStatusCode();
        var safetyPreview = await Preview(site, await safety.Content.ReadAsByteArrayAsync()); safetyPreview.EnsureSuccessStatusCode();
        Assert.Equal(2, (await safetyPreview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("backupAccounts").GetInt32());
        await using var control = await site.Storage.factory.CreateDbContextAsync(); Assert.Single(await control.HouseholdMembers.ToListAsync()); Assert.Single(await control.Users.ToListAsync());
    }
    [PostgresFact] public async Task Portable_restore_crosses_installations_and_csv_export_excludes_identity()
    {
        await using var source = new HybridApiIntegrationTests.Fixture(); await Setup(source, "portable-source");
        await using var target = new HybridApiIntegrationTests.Fixture(); await Setup(target, "portable-target", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await Preview(target, await Backup(source))).StatusCode);
        var portable = await Backup(source, true); var preview = await Preview(target, portable, true); preview.EnsureSuccessStatusCode();
        var id = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("previewId").GetGuid();
        var confirmed = await target.Client.PostAsJsonAsync("/api/household-data/restore/confirm", new { previewId = id, currentPassword = "Synthetic!2026", confirmReplacement = true });
        Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
        var export = await target.Client.PostAsJsonAsync("/api/household-data/export", new { currentPassword = "Synthetic!2026" }); export.EnsureSuccessStatusCode();
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
        Assert.NotNull(zip.GetEntry("Accounts.csv")); Assert.NotNull(zip.GetEntry("Transactions.csv")); Assert.DoesNotContain(zip.Entries, x => x.Name.Contains("AspNet") || x.Name.Contains("HouseholdMembers"));
        await using var db = await target.Storage.factory.CreateDbContextAsync(); Assert.True(await db.Users.AnyAsync(x => x.Email == "portable-target@coiny.test")); Assert.Equal(2, await db.Households.CountAsync());
    }
    [PostgresFact] public async Task Members_and_malformed_authenticated_backups_cannot_replace_data()
    {
        await using var site = new HybridApiIntegrationTests.Fixture(); await Setup(site, "security-owner");
        var ownerToken = site.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var portable = await Backup(site, true);
        var crypto = new Coiny.API.Services.Recovery.BackupEncryption(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var payload = System.Text.Json.Nodes.JsonNode.Parse(crypto.Decrypt(portable, "Portable backup passphrase").Plain)!;
        var file = Path.Combine(Path.GetTempPath(), "malformed-household-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try {
            await File.WriteAllBytesAsync(file, Convert.FromBase64String(payload["Database"]!.GetValue<string>()));
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + file + ";Pooling=false")) {
                await connection.OpenAsync(); await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TRIGGER unexpected AFTER INSERT ON Accounts BEGIN SELECT 1; END"; await command.ExecuteNonQueryAsync();
            }
            var bytes = await File.ReadAllBytesAsync(file); payload["Database"] = System.Text.Json.Nodes.JsonValue.Create(bytes); payload["Sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var malformed = crypto.Encrypt(JsonSerializer.SerializeToUtf8Bytes(payload), "Portable backup passphrase");
            Assert.Equal(HttpStatusCode.BadRequest, (await Preview(site, malformed, true)).StatusCode);
            Assert.Single((await site.Client.GetFromJsonAsync<JsonElement>("/api/accounts")).EnumerateArray());
        } finally { if (File.Exists(file)) File.Delete(file); }
        var member = await site.Register("data-member");
        await using (var db = await site.Storage.factory.CreateDbContextAsync()) {
            db.Add(new HouseholdMember { HouseholdId = (await db.Households.SingleAsync()).Id, UserId = (await db.Users.SingleAsync(x => x.Email == "data-member@coiny.test")).Id, Role = Coiny.API.Enums.HouseholdRole.Member }); await db.SaveChangesAsync();
        }
        site.Use(member);
        Assert.Equal(HttpStatusCode.Forbidden, (await site.Client.PostAsJsonAsync("/api/household-data/backup", new { currentPassword = "Synthetic!2026", passwordProtected = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await site.Client.PostAsJsonAsync("/api/household-data/export", new { currentPassword = "Synthetic!2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Preview(site, portable, true)).StatusCode);
        site.Use(ownerToken); Assert.Single((await site.Client.GetFromJsonAsync<JsonElement>("/api/accounts")).EnumerateArray());
    }

}
