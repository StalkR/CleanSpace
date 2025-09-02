using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shared.Plugin;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using VRage.Utils;

namespace TorchPlugin.Hasher
{

    internal sealed class HasherFactory
    {

        private static String[] slots = { "  using (var sha25 = SHA256.Create())\r\n            sb.AppendLine(Convert.ToBase64String(sha25.ComputeHash(raw)));", "foreach (var mod in asm.GetModules())\r\n        {\r\n            sb.AppendLine(mod.ScopeName);\r\n            sb.AppendLine(mod.MDStreamVersion.ToString());\r\n        }", "foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))\r\n        {\r\n            foreach (var method in type.GetMethods(BindingFlags.Public |\r\n                                                   BindingFlags.NonPublic |\r\n                                                   BindingFlags.Static |\r\n                                                   BindingFlags.Instance))\r\n            {\r\n                sb.AppendLine(method.ToString());\r\n                var body = method.GetMethodBody();\r\n                if (body != null)\r\n                    sb.AppendLine(BitConverter.ToString(body.GetILAsByteArray() ?? Array.Empty<byte>()));\r\n                IntPtr fnPtr = method.MethodHandle.GetFunctionPointer();\r\n                sb.AppendLine(fnPtr.ToString(\"X\"));\r\n            }\r\n        }" };
        private static String hasherTemplate(string secretSlot, string slot1, string slot2, string slot3) => "using System;\r\nusing System.IO;\r\nusing System.Linq;\r\nusing System.Reflection;\r\nusing System.Security.Cryptography;\r\nusing System.Text;\r\npublic static class Hasher\r\n{\r\n    public static byte[] GetAssemblyBytes(Assembly asm)\r\n    {\r\n        string location = asm.Location;\r\n        if (!string.IsNullOrEmpty(location) && File.Exists(location))\r\n            return File.ReadAllBytes(location);\r\n\r\n        using (var stream = asm.ManifestModule.FullyQualifiedName != null\r\n            ? File.OpenRead(asm.ManifestModule.FullyQualifiedName)\r\n            : asm.ManifestModule.Assembly.GetManifestResourceStream(asm.ManifestModule.ScopeName))\r\n        {\r\n            if (stream == null)\r\n                return Array.Empty<byte>();\r\n            using (var ms = new MemoryStream())\r\n            {\r\n                stream.CopyTo(ms);\r\n                return ms.ToArray();\r\n            }\r\n        }\r\n    }\r\n\r\n    public static string ComputeHash()\r\n    {\r\n        byte[] secret = Encoding.UTF8.GetBytes(\"" + secretSlot + "\");\r\n\r\n        var asm = Assembly.GetExecutingAssembly();\r\n        var sb = new StringBuilder();\r\n        var raw = GetAssemblyBytes(asm);\r\n        " + slot1 + "\r\n        " + slot2 + "\r\n        " + slot3 + "\r\n\r\n        byte[] hash;\r\n        using (SHA256 sha256 = SHA256.Create())\r\n        {\r\n            hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));\r\n        }\r\n\r\n        byte[] encrypted = new byte[hash.Length];\r\n        for (int i = 0; i < hash.Length; i++)\r\n        {\r\n            encrypted[i] = (byte)(hash[i] ^ secret[i % secret.Length]);\r\n        }\r\n\r\n        return Convert.ToBase64String(encrypted);\r\n    }\r\n}\r\n";
        private static String MakeHasher(string secret)
        {
            slots.ShuffleList();
            return hasherTemplate(secret, slots[0], slots[1], slots[2]);
        }
        private static byte[] CompileHasherAssembly(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var refs = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(SHA256).Assembly.Location),
        };

            var compilation = CSharpCompilation.Create(
                assemblyName: "DynamicHasher_" + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { syntaxTree },
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var emitResult = compilation.Emit(ms);
                if (!emitResult.Success)
                {
                    var diag = string.Join("\n", emitResult.Diagnostics.Select(d => d.ToString()));
                    throw new InvalidOperationException("Compilation failed: " + diag);
                }
                return ms.ToArray();
            }
        }

        public static byte[] GetNewHasherWithSecret(string secret)
        {
            return CompileHasherAssembly(MakeHasher(secret)).ToArray();
        }

        public static MethodInfo TryGetHashMethodFrom(byte[] bytecode)
        {
            Assembly clientHasher = Assembly.Load(bytecode);
            try
            {
                Type hasherType = clientHasher.GetType("Hasher");
                if (hasherType == null)
                {
                    throw new Exception("Couldn't extract a valid hash computation method from bytecode. ");
                }
                MethodInfo computeHash = hasherType.GetMethod("ComputeHash", BindingFlags.Public | BindingFlags.Static);
                return computeHash;
            }
            catch
            {
                throw new Exception("Couldn't extract a valid hash computation method from bytecode. ");
            }
        }
    }
}

