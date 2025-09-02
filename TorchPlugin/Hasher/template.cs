using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
public static class Hasher
{
    private const string Nonce = "poop";
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
        byte[] secret = Encoding.UTF8.GetBytes(Nonce);

        var asm = Assembly.GetExecutingAssembly();
        var sb = new StringBuilder();
        var raw = GetAssemblyBytes(asm);
        using (var sha25 = SHA256.Create())
            sb.AppendLine(Convert.ToBase64String(sha25.ComputeHash(raw)));
        foreach (var mod in asm.GetModules())
        {
            sb.AppendLine(mod.ScopeName);
            sb.AppendLine(mod.MDStreamVersion.ToString());
        }
        foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public |
                                                   BindingFlags.NonPublic |
                                                   BindingFlags.Static |
                                                   BindingFlags.Instance))
            {
                sb.AppendLine(method.ToString());
                var body = method.GetMethodBody();
                if (body != null)
                    sb.AppendLine(BitConverter.ToString(body.GetILAsByteArray() ?? Array.Empty<byte>()));
                IntPtr fnPtr = method.MethodHandle.GetFunctionPointer();
                sb.AppendLine(fnPtr.ToString("X"));
            }
        }

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
