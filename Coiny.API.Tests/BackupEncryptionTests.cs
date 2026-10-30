using Coiny.API.Services.Recovery;
using Microsoft.AspNetCore.DataProtection;
namespace Coiny.API.Tests;
public class BackupEncryptionTests
{
    [Fact] public void Portable_files_cross_installations_but_wrong_password_tamper_and_truncation_fail()
    {
        var first = new BackupEncryption(new EphemeralDataProtectionProvider()); var other = new BackupEncryption(new EphemeralDataProtectionProvider());
        var file = first.Encrypt([1, 2, 3], "A private backup password");
        Assert.Equal(new byte[] { 1, 2, 3 }, other.Decrypt(file, "A private backup password").Plain);
        Assert.True(other.Decrypt(file, "A private backup password").Portable);
        Assert.NotEqual(file, first.Encrypt([1, 2, 3], "A private backup password"));
        Assert.Throws<ArgumentException>(() => other.Decrypt(file, "wrong"));
        Assert.Throws<ArgumentException>(() => other.Decrypt(file[..^1], "A private backup password"));
        file[^1] ^= 1; Assert.Throws<ArgumentException>(() => other.Decrypt(file, "A private backup password"));
        Assert.Throws<ArgumentException>(() => first.Encrypt([1], "short"));
    }
    [Fact] public void Linked_files_require_installation_keys_and_csv_formulas_are_escaped()
    {
        var first = new BackupEncryption(new EphemeralDataProtectionProvider()); var other = new BackupEncryption(new EphemeralDataProtectionProvider());
        var file = first.Encrypt([1, 2, 3], null);
        Assert.False(first.Decrypt(file, null).Portable); Assert.Throws<ArgumentException>(() => other.Decrypt(file, null));
        Assert.Equal("\"'=HYPERLINK(\"\"bad\"\")\"", HouseholdRecoveryService.Csv("=HYPERLINK(\"bad\")"));
        Assert.Equal("\"-12.30\"", HouseholdRecoveryService.Csv("-12.30"));
    }
}
