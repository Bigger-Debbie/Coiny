using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
namespace Coiny.API.Services.Recovery;

// Versioned file envelope. Crypto operations are framework primitives, not custom algorithms.
public sealed class BackupEncryption(IDataProtectionProvider protection)
{
    public const int MaximumFile = 64 * 1024 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("COINYB01");
    private const int Iterations = 600_000;
    public byte[] Encrypt(byte[] plain, string? password)
    {
        if (plain.Length > MaximumFile - 1024) throw new ArgumentException("This household backup exceeds the supported size.");
        if (password is null) return [..Magic, 1, ..protection.CreateProtector("Coiny.HouseholdBackup.v1").Protect(plain)];
        if (password.Length is < 12 or > 1024) throw new ArgumentException("Use a backup password with 12 to 1,024 characters.");
        var salt = RandomNumberGenerator.GetBytes(16); var nonce = RandomNumberGenerator.GetBytes(12);
        byte[] header = [..Magic, 2, ..salt, ..nonce];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try {
            var cipher = new byte[plain.Length]; var tag = new byte[16];
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, header);
            return [..header, ..tag, ..cipher];
        } finally { CryptographicOperations.ZeroMemory(key); }
    }
    public (byte[] Plain, bool Portable) Decrypt(byte[] file, string? password)
    {
        if (file.Length < 10 || file.Length > MaximumFile || !file.AsSpan(0, 8).SequenceEqual(Magic))
            throw new ArgumentException("Choose a supported Coiny backup file.");
        try {
            if (file[8] == 1) return (protection.CreateProtector("Coiny.HouseholdBackup.v1").Unprotect(file[9..]), false);
            if (file[8] != 2 || file.Length < 53 || string.IsNullOrEmpty(password) || password.Length > 1024) throw new CryptographicException();
            var key = Rfc2898DeriveBytes.Pbkdf2(password, file.AsSpan(9, 16), Iterations, HashAlgorithmName.SHA256, 32);
            try {
                var plain = new byte[file.Length - 53];
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(file.AsSpan(25, 12), file.AsSpan(53), file.AsSpan(37, 16), plain, file.AsSpan(0, 37));
                return (plain, true);
            } finally { CryptographicOperations.ZeroMemory(key); }
        } catch (CryptographicException) { throw new ArgumentException("The backup could not be unlocked. Check its password or use the original installation and recovery keys."); }
    }
}
