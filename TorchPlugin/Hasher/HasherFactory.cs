using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
                "}",

                "foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))\r\n" +
                "{\r\n" +
                "    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))\r\n" +
                "    {\r\n" +
                "        try\r\n" +
                "        {\r\n" +
                "            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(method.MethodHandle);\r\n" +
                "            var ptr = method.MethodHandle.GetFunctionPointer();\r\n" +
                "            bool hasIL = method.GetMethodBody()?.GetILAsByteArray()?.Length > 0;\r\n" +
                "            bool detoured = hasIL && ptr == IntPtr.Zero;\r\n" +
                "            sb.AppendLine($\"{method} :: Detoured={detoured}\");\r\n" +
                "        }\r\n" +
                "        catch (Exception ex)\r\n" +
                "        {\r\n" +
                "            sb.AppendLine($\"{method} :: Error={ex.Message}\");\r\n" +
                "        }\r\n" +
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

                    var asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.FullName?.IndexOf(""CleanSpace"", StringComparison.OrdinalIgnoreCase) >= 0);

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

        private static string ServerHasherTemplate(string secretSlot, string slot1, string slot2, string slot3) =>
            @"using System;
            using System.Linq;
            using System.Reflection;
            using System.Security.Cryptography;
            using System.Text;

            public static class Hasher
            {
                public static string ComputeHash(Assembly t)
                {
                    byte[] secret = Encoding.UTF8.GetBytes(""" + secretSlot + @""");

                    var asm = t;
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
        public static (String, int[]) MakeHasher(string secret, int[] indexes = null)
        {
            if (indexes == null)
            {
                indexes = new int[Slots.Length];
                foreach (var i in indexes)
                    indexes[i] = i;

                indexes.ShuffleList();
                return (HasherTemplate(secret, Slots[indexes[0]], Slots[indexes[1]], Slots[indexes[2]]), indexes);
            }
            return (ServerHasherTemplate(secret, Slots[indexes[0]], Slots[indexes[1]], Slots[indexes[2]]), indexes);
        }

        public static byte[] CompileHasherAssembly(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
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

        public static (byte[], int[]) GetNewHasherWithSecret(string secret)
        {
            (var hasherBytes, int[] indexes) = MakeHasher(secret);
            return (CompileHasherAssembly(hasherBytes).ToArray(), indexes);
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

        public void ValidateSourceSyntaxOrThrow(string source)
        {
            var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp7_3));
            var root = (CompilationUnitSyntax)tree.GetRoot();
            var allowedUsings = new HashSet<string>{
                    "System","System.IO","System.Linq","System.Reflection",
                    "System.Security.Cryptography","System.Text"
                };
            if (root.Usings.Any(u => !allowedUsings.Contains(u.Name.ToString())))
                throw new InvalidOperationException("Disallowed using directive.");

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            if (classes.Length != 1) throw new InvalidOperationException("Unexpected type count.");
            var cls = classes[0];
            if (cls.Identifier.Text != "Hasher") throw new InvalidOperationException("Class must be Hasher.");
            var mods = cls.Modifiers.Select(m => m.Text).ToHashSet();
            if (!mods.Contains("public") || !mods.Contains("static"))
                throw new InvalidOperationException("Class must be public static.");

            if (cls.AttributeLists.Count > 0) throw new InvalidOperationException("Attributes not allowed.");
            if (cls.Members.OfType<TypeDeclarationSyntax>().Any())
                throw new InvalidOperationException("Nested types not allowed.");

            var methods = cls.Members.OfType<MethodDeclarationSyntax>().ToArray();
            if (methods.Length != 1) throw new InvalidOperationException("Unexpected method count.");

            bool HasMethod(string ret, string name, params (string type, string id)[] ps)
            {
                var m = methods.FirstOrDefault(x => x.Identifier.Text == name);
                if (m == null) return false;
                if (m.ReturnType.ToString() != ret) return false;
                var parms = m.ParameterList.Parameters.Select(p => (p.Type?.ToString(), p.Identifier.Text)).ToArray();
                if (parms.Length != ps.Length) return false;
                for (int i = 0; i < ps.Length; i++)
                    if (ps[i].type != parms[i].Item1 || ps[i].id != parms[i].Item2) return false;
                if (m.Modifiers.Any(mm => (mm.Text == "unsafe" || mm.Text == "extern" || mm.Text == "async")))
                    return false;
                if (m.AttributeLists.Count > 0) return false;
                return true;
            }

            if (!HasMethod("string", "ComputeHash"))
                throw new InvalidOperationException("ComputeHash signature mismatch.");

            var forbidden = new[] { SyntaxKind.UnsafeStatement, SyntaxKind.FixedStatement, SyntaxKind.StackAllocArrayCreationExpression };
            if (root.DescendantNodes().Any(n => forbidden.Contains(n.Kind())))
                throw new InvalidOperationException("Unsafe constructs not allowed.");

        }
    }
}

