using Shared.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CleanSpaceShared.Scanner
{
     public static class SigScanner
    {   
        public static IPluginLogger Logger;
        public static string[] GetSignatures(Assembly asm, int num=15)
        {
            var sb = new string[15];
            try
            {
                var types = asm.GetTypes().Where(t => t.IsClass || t.IsValueType || t.IsInterface).OrderBy(t => t.FullName);

                int i = 0;
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Static | BindingFlags.Instance |
                                                    BindingFlags.DeclaredOnly).OrderBy(m => m.ToString());
                    foreach (var method in methods){
                        sb[i] = FormatMethodSignature(method);
                        ++i;
                        if (i >= num)
                            break;
                    }
                    if (i >= num)
                        break;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Logger?.Error($"TypeLoadError while calculating hash: {ex.Message}");
                foreach (var loaderEx in ex.LoaderExceptions)
                    Logger?.Error(loaderEx.ToString());
                return ex.Types.Where(t => t != null && (t.IsClass || t.IsValueType || t.IsInterface))
                    .OrderBy(t => t.FullName).Take(num).SelectMany(t =>
                    {
                        try
                        {
                            return t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Static | BindingFlags.Instance |
                                                BindingFlags.DeclaredOnly)
                                    .OrderBy(m => m.ToString()).Take(num).Select(FormatMethodSignature);
                        }
                        catch (Exception innerEx)
                        {
                            Logger?.Warning($"Failed to inspect type {t.FullName}: {innerEx}");
                            return Enumerable.Empty<string>();
                        }
                    }).ToArray();
            }
            return sb;
        }


        public static string FormatMethodSignature(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append(method.ReturnType?.FullName ?? "<null>");
            sb.Append(" ");
            sb.Append(method.DeclaringType != null ? method.DeclaringType.FullName : "<NoDeclaringType>");
            sb.Append("::");
            sb.Append(method.Name);
            sb.Append("(");
            sb.Append(string.Join(", ", method.GetParameters()
                .Select(p => p.ParameterType?.FullName ?? "<null>")));
            sb.Append(")");
            return sb.ToString();
        }

        public static string HashSignatures(string signatures)
        {
            var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(signatures);
            var hash = sha256.ComputeHash(bytes);       
            return BitConverter.ToString(hash).Replace("-", "");            
        }

        public static string getCompleteAssemblyHash(Assembly a)
        {
            string[] signatures = GetSignatures(a, 15);
            return HashSignatures(string.Join("\n", signatures.Where(s => s != null)));
        }
    }

}
