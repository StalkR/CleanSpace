using NLog;
using Shared.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using VRage.Plugins;

namespace CleanSpaceShared.Scanner
{    
    internal sealed class AssemblyScanner
    {
        public static IPluginLogger Logger;
        public static bool IsUnsigned(Assembly a) => a.GetName().GetPublicKeyToken()?.Length == 0;
        public static List<Assembly> GetPluginAssemblies()
        {
            List<Assembly> assemblies = new List<Assembly>();
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where((asm)=> IsValidPlugin(asm)).ToList();
        }
        internal struct PluginAssemblyInfo
        {
            public string Name;
            public string Hash;
            public Assembly Assembly;
        }
        public static List<PluginAssemblyInfo> GetRawPluginAssembliesData()
        {
            List<PluginAssemblyInfo> result = new List<PluginAssemblyInfo>();
            List<Assembly> assemblies = new List<Assembly>();
            var validAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where((asm) => IsValidPlugin(asm)).ToList();
            return validAssemblies.Select((asm) => new PluginAssemblyInfo() { Assembly = asm, Hash = GetAssemblyFingerprint(asm), Name = asm.GetName().Name }).ToList();
        }

        public static Assembly GetAssembly(string path)
        {
            if (File.Exists(path))
            {
                Assembly a = Assembly.LoadFile(path);               
                return a;
            }
            return null;
        }

        public static string GetOwnHash() => GetAssemblyFingerprint(GetOwnAssembly());
        public static Assembly GetOwnAssembly() => Assembly.GetExecutingAssembly();
        public static bool IsValidPlugin(Assembly assembly)
        {
            if (assembly.FullName.Contains("Sandbox.Game, Version")) return false;
            Type[] types;
                        
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
                                                                 
            }
            catch
            {             
                return false;
            }
            return types.Any(t => typeof(IPlugin).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
        }
        public static bool IsValidPlugin(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                return false;

            try
            {
                var assembly = Assembly.LoadFile(dllPath);
                return IsValidPlugin(assembly);
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (FileLoadException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($" Failed to load {dllPath}: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public static bool ValidateAssemblyIntegrity(Assembly a, string[] allowedHashes) => allowedHashes.Contains(GetAssemblyFingerprint(a));

        public static string GetAssemblyFingerprint(Assembly assembly)
        {
            var sb = new StringBuilder();
            var name = assembly.GetName();
            sb.AppendLine(name.Name);
            sb.AppendLine(name.Version.ToString());
            sb.AppendLine(BitConverter.ToString(name.GetPublicKeyToken() ?? new byte[0]));

            var types = assembly.GetTypes().OrderBy(t => t.FullName);
            foreach (var type in types)
            {
                sb.AppendLine(type.FullName);
                sb.AppendLine(type.Attributes.ToString());
                foreach (var field in type.GetFields().OrderBy(f => f.Name))
                    sb.AppendLine($"{field.Name}:{field.FieldType}");

                foreach (var method in type.GetMethods().OrderBy(m => m.Name))
                {
                    sb.AppendLine(method.ToString());
                    var body = method.GetMethodBody();
                    if (body != null)
                    {
                        var il = body.GetILAsByteArray();
                        if (il != null)
                            sb.AppendLine(BitConverter.ToString(il));
                    }
                }
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return Convert.ToBase64String(hash);
            }
        }

        public static string UnscrambleSecureFingerprint(string encoded, byte[] secret)
        {
            byte[] encrypted = Convert.FromBase64String(encoded);
            byte[] hash = new byte[encrypted.Length];
            for (int i = 0; i < encrypted.Length; i++)
                hash[i] = (byte)(encrypted[i] ^ secret[i % secret.Length]);
            return Convert.ToBase64String(hash);
        }

        public static string GetSecureAssemblyFingerprint(Assembly assembly, byte[] secret)
        {
            var sb = new StringBuilder();
            var name = assembly.GetName();
            sb.AppendLine(name.Name);
            sb.AppendLine(name.Version.ToString());
            sb.AppendLine(BitConverter.ToString(name.GetPublicKeyToken() ?? new byte[0]));

            var types = assembly.GetTypes().OrderBy(t => t.FullName);
            foreach (var type in types)
            {
                sb.AppendLine(type.FullName);
                sb.AppendLine(type.Attributes.ToString());
                foreach (var field in type.GetFields().OrderBy(f => f.Name))
                    sb.AppendLine($"{field.Name}:{field.FieldType}");

                foreach (var method in type.GetMethods().OrderBy(m => m.Name))
                {
                    sb.AppendLine(method.ToString());
                    var body = method.GetMethodBody();
                    if (body != null)
                    {
                        var il = body.GetILAsByteArray();
                        if (il != null)
                            sb.AppendLine(BitConverter.ToString(il));
                    }
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
}
