
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Shared.Hasher
{
    public class HasherRunner
    {
        static readonly HashSet<string> AllowedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mscorlib",
                "System.Core",
                "System.Private.CoreLib",
                "System.Linq",
                "System.Reflection",
                "System.Security.Cryptography.Algorithms",
                "System.Text"
            };

        public static void ValidateHasherRunnerBytes(byte[] dllBytes)
        {
            using (var ms = new MemoryStream(dllBytes))
            {
                using (var pe = new PEReader(ms, PEStreamOptions.PrefetchEntireImage))
                {
                    if (!pe.HasMetadata) throw new InvalidOperationException("Not a managed assembly.");
                    var md = pe.GetMetadataReader();

                    foreach (var handle in md.MethodDefinitions)
                    {
                        var method = md.GetMethodDefinition(handle);
                        if ((method.Attributes & System.Reflection.MethodAttributes.PinvokeImpl) != 0)
                        {
                            var import = method.GetImport();
                            var moduleName = md.GetModuleReference(import.Module).Name;
                            var entryPoint = md.GetString(import.Name);
                            throw new InvalidOperationException($"Disallowed P/Invoke: {moduleName}!{entryPoint}");
                        }
                    }

                    foreach (var h in md.AssemblyReferences)
                    {
                        var aref = md.GetAssemblyReference(h);
                        var name = md.GetString(aref.Name); 

                        if (!AllowedAssemblyNames.Contains(name))
                        {
                            throw new InvalidOperationException($"Disallowed AssemblyRef: {name}");
                        }
                    }

                    var hasher = md.TypeDefinitions
                        .Select(h => md.GetTypeDefinition(h))
                        .Where(t => md.GetString(t.Name) == "Hasher" && md.GetString(t.Namespace) == "")
                        .ToArray();
                    if (hasher.Length != 1) throw new InvalidOperationException("Exactly one Hasher type required.");
                    var ht = hasher[0];

                    var methods = ht.GetMethods().Select(h => md.GetMethodDefinition(h)).ToArray();
                    if (methods.Length != 1) throw new InvalidOperationException("Exactly one method required.");

                    byte[] GetMethodBodyIL(System.Reflection.Metadata.MethodDefinition mdef)
                    {
                        var body = pe.GetMethodBody(mdef.RelativeVirtualAddress);
                        return body.GetILBytes().ToArray();
                    }

                    var sigs = new HashSet<string>();
                    foreach (var m in methods)
                    {
                        var name = md.GetString(m.Name);
                        var sig = md.GetBlobReader(m.Signature).ReadSerializedString();
                        sigs.Add(name);

                        if ((m.Attributes & MethodAttributes.PinvokeImpl) != 0)
                            throw new InvalidOperationException("PinvokeImpl not allowed.");
                        if ((m.ImplAttributes & MethodImplAttributes.Unmanaged) != 0)
                            throw new InvalidOperationException("Unmanaged impl not allowed.");

                        // 4b) IL opcode whitelist enforcement
                        var il = GetMethodBodyIL(m);
                        ValidateOpcodeWhitelist(il);
                    }

                    if (!sigs.SetEquals(new[] { "ComputeHash" }))
                        throw new InvalidOperationException("Unexpected method names.");
                }
            }
        }

        static void ValidateOpcodeWhitelist(byte[] il)
        {            
                    var disallowed = new HashSet<ushort> {
                0x27, /* Jmp */
                0xFE09, /* Unaligned */
                0xFE0A, /* Volatile */
                0xFE1A, /* Cpblk */
                0xFE18, /* Initblk */
                0xD1,   /* Localloc */
                0x29,   /* Calli */
                0xFE14  /* Tail. */
            };
            int i = 0;
            while (i < il.Length)
            {
                ushort op = il[i++];
                if (op == 0xFE) { op = (ushort)(0xFE00 | il[i++]); }
                if (disallowed.Contains(op))
                    throw new InvalidOperationException($"Disallowed IL opcode: 0x{op:X}");
                // TODO: use a real IL reader (Mono.Cecil or write a small decoder).
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

        public static string ExecuteRunner(byte[] assemblyBytes, params object[] args)
        {
            if (assemblyBytes == null || assemblyBytes.Length == 0)
                throw new ArgumentException("Assembly bytes cannot be null or empty.", nameof(assemblyBytes));
            ValidateHasherRunnerBytes(assemblyBytes);
            var assembly = Assembly.Load(assemblyBytes);
            var targetType = assembly.GetTypes()
                .FirstOrDefault(t => t.GetMethod("ComputeHash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance) != null);
           
            if (targetType == null)
                throw new MissingMethodException("Could not find any type containing a method named 'ComputeHash'.");

            var method = targetType.GetMethod("ComputeHash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (method == null)
                throw new MissingMethodException("Could not find method 'ComputeHash'.");

            object instance = null;
            if (!method.IsStatic)
                instance = Activator.CreateInstance(targetType);
            var result = method.Invoke(instance, args);

            return result?.ToString();
        }
    }
}