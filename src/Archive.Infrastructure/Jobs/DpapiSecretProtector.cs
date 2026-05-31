using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace Archive.Infrastructure.Jobs;

[SupportedOSPlatform("windows")]
internal static class DpapiSecretProtector
{
    public static string Protect(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);
        var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedData);
    }

    public static string Unprotect(string protectedBase64)
    {
        var protectedData = Convert.FromBase64String(protectedBase64);
        var data = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }
}
