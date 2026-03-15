using System.Security.Cryptography;
using System.Text;

namespace IssuePit.Core;

/// <summary>
/// Shared hashing utilities.
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Computes the SHA-256 hash of the given UTF-8 string and returns it as a lowercase hex string.
    /// </summary>
    public static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
