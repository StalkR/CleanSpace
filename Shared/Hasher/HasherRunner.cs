
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
using System.Runtime.CompilerServices;

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

        [MethodImpl(MethodImplOptions.NoInlining)]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
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

        [MethodImpl(MethodImplOptions.NoInlining)]
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