using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shared.Plugin;
using System;
using System.Collections.Generic;
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

        private static readonly string[] Slots = new[]{
                "foreach (var mod in asm.GetModules())\r\n" +
                "{\r\n" +
                "    sb.AppendLine(mod.ScopeName);\r\n" +
                "    sb.AppendLine(mod.MDStreamVersion.ToString());\r\n" +
                "}",

                "foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))\r\n" +
                "{\r\n" +
                "    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))\r\n" +
                "    {\r\n" +
                "        sb.AppendLine(method.ToString());\r\n" +
                "        var body = method.GetMethodBody();\r\n" +
                "        if (body != null)\r\n" +
                "            sb.AppendLine(BitConverter.ToString(body.GetILAsByteArray() ?? Array.Empty<byte>()));\r\n" +
                "    }\r\n" +
                "}",

                "foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))\r\n" +
                "{\r\n" +
                "    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))\r\n" +
                "    {\r\n" +
                "        sb.AppendLine($\"{field.FieldType} {field.Name}\");\r\n" +
                "    }\r\n" +
                "}",

                "foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))\r\n" +
                "{\r\n" +
                "    foreach (var attr in type.GetCustomAttributes(false))\r\n" +
                "    {\r\n" +
                "        sb.AppendLine(attr.GetType().FullName);\r\n" +
                "    }\r\n" +
                "}",

                "foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))\r\n" +
                "{\r\n" +
                "    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))\r\n" +
                "    {\r\n" +
                "        sb.AppendLine($\"{prop.PropertyType} {prop.Name}\");\r\n" +
                "        foreach (var accessor in prop.GetAccessors(true))\r\n" +
                "            sb.AppendLine(accessor.ToString());\r\n" +
                "    }\r\n" +
                "}"
            };

       private static string HasherTemplate(string secretSlot, string slot1, string slot2, string slot3) =>
             @"using System;
            using System.Linq;
            using System.Reflection;
            using System.Security.Cryptography;
            using System.Text;

            public static class Hasher
            {
                public static string ComputeHash()
                {
                    byte[] secret = Encoding.UTF8.GetBytes(""" + secretSlot + @""");

                    var asm = Assembly.GetExecutingAssembly();
                    var sb = new StringBuilder();

                    " + slot1 + @"
                    " + slot2 + @"
                    " + slot3 + @"

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
            }";
        private static String MakeHasher(string secret)
        {
            Slots.ShuffleList();
            return HasherTemplate(secret, Slots[0], Slots[1], Slots[2]);
        }
        private static byte[] CompileHasherAssembly(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            /*var refs = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .GroupBy(a => a.GetName().Name)             
                .Select(g => g.First())  
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();*/
            var refs = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),          // System.Private.CoreLib
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),      // System.Linq
                MetadataReference.CreateFromFile(typeof(Assembly).Assembly.Location),        // System.Reflection
                MetadataReference.CreateFromFile(typeof(SHA256).Assembly.Location),          // System.Security.Cryptography.Algorithms
                MetadataReference.CreateFromFile(typeof(StringBuilder).Assembly.Location),   // System.Text
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

