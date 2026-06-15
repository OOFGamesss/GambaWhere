using System;
using System.IO;
using System.Security.Cryptography;

namespace GambaWhere.Utility;

/// <summary>Reads a stored profile picture into the base64 + hash the API expects.</summary>
public static class ProfileImageEncoder
{
    public static bool TryEncode(string path, out string base64, out string hash)
    {
        base64 = string.Empty;
        hash = string.Empty;

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > Images.ProfileImageStore.MaxBytes)
                return false;

            var bytes = File.ReadAllBytes(path);
            base64 = Convert.ToBase64String(bytes);
            hash = Convert.ToHexString(SHA256.HashData(bytes));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
