using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using SecureArchive.Utils.Crypto;

namespace SecureArchive.Utils.Crypto;
internal static class HashHelper
{
    public static IHashResult SHA256(string password, string seed)
    {
        return HashBuilder
            .SHA256
            .Append(seed)
            .Append(password)
            .Build();
    }

    public static IBuffer AsBuffer(this string src) {
        return Encoding.UTF8.GetBytes(src).AsBuffer();
    }

    public static string EncodeBase64(this IBuffer source) {
        return CryptographicBuffer.EncodeToBase64String(source);
    }
    public static string EncodeBase64(this byte[] source) {
        return CryptographicBuffer.EncodeToBase64String(source.AsBuffer());
    }
    public static string EncodeHexString(this IBuffer source) {
        return CryptographicBuffer.EncodeToHexString(source);
    }
    public static string EncodeHexString(this byte[] source) {
        return CryptographicBuffer.EncodeToHexString(source.AsBuffer());
    }

    public static IBuffer DecodeBase64(this string source) {
        return CryptographicBuffer.DecodeFromBase64String(source);
    }
    public static IBuffer DecodeHexString(this string source) {
        return CryptographicBuffer.DecodeFromHexString(source);
    }
}
