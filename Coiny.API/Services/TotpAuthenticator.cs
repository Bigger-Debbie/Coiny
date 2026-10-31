using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Coiny.API.Services;

public static class TotpAuthenticator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    public static string NewKey() => Encode(RandomNumberGenerator.GetBytes(20));
    public static string Encode(byte[] bytes)
    {
        var buffer = 0; var bits = 0; var result = new System.Text.StringBuilder();
        foreach (var b in bytes) {
            buffer = (buffer << 8) | b; bits += 8;
            while (bits >= 5) { bits -= 5; result.Append(Alphabet[(buffer >> bits) & 31]); }
        }
        if (bits > 0) result.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return result.ToString();
    }
    private static byte[] Decode(string text)
    {
        var output = new List<byte>(); var buffer = 0; var bits = 0;
        foreach (var c in text) {
            var value = Alphabet.IndexOf(c); if (value < 0) throw new FormatException("Invalid authenticator key.");
            buffer = (buffer << 5) | value; bits += 5;
            if (bits >= 8) { bits -= 8; output.Add((byte)(buffer >> bits)); }
        }
        return output.ToArray();
    }
    public static string Code(string key, long step)
    {
        Span<byte> message = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(message, step);
        var hash = HMACSHA1.HashData(Decode(key), message); var offset = hash[^1] & 15;
        return ((BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7fffffff) % 1_000_000).ToString("D6");
    }
    public static long? Verify(string key, string code, DateTimeOffset now)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) return null;
        var step = now.ToUnixTimeSeconds() / 30;
        for (var delta = -1; delta <= 1; delta++)
            if (CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(Code(key, step + delta)), System.Text.Encoding.ASCII.GetBytes(code))) return step + delta;
        return null;
    }
}
