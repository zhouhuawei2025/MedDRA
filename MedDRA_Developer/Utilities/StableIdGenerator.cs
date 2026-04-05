using System.Security.Cryptography;
using System.Text;

namespace MedDRA_Developer.Utilities;

internal static class StableIdGenerator
{
    public static ulong From(string version, string lltCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{version}_{lltCode}"));
        return BitConverter.ToUInt64(bytes, 0);
    }
}
