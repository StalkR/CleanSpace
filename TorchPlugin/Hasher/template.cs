using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
public static class Hasher
{
    public static byte[] GetAssemblyBytes(Assembly asm)
    {
        string location = asm.Location;
        if (!string.IsNullOrEmpty(location) && File.Exists(location))
            return File.ReadAllBytes(location);

        using (var stream = asm.ManifestModule.FullyQualifiedName != null
            ? File.OpenRead(asm.ManifestModule.FullyQualifiedName)
            : asm.ManifestModule.Assembly.GetManifestResourceStream(asm.ManifestModule.ScopeName))
        {
            if (stream == null)
                return Array.Empty<byte>();
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }

    public static string ComputeHash()
    {
        byte[] secret = Encoding.UTF8.GetBytes("{secretSlot}");

        var asm = Assembly.GetExecutingAssembly();
        var sb = new StringBuilder();
        var raw = GetAssemblyBytes(asm);
       // { slot1}
      //  { slot2}
      //  { slot3}

        byte[] hash;
        using (SHA256 sha256 = SHA256.Create())
        {
            hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        byte[] encrypted = new byte[hash.Length];
        for (int i = 0; i < hash.Length; i++)
        {
            encrypted[i] = (byte)(hash[i] ^ secret[i % secret.Length]);
        }

        return Convert.ToBase64String(encrypted);
    }
}
