using NLog;
using Shared.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VRage.Plugins;

namespace CleanSpaceShared.Scanner
{    
    public class AssemblyScanner
    {
        public static IPluginLogger Logger;
        public static bool IsUnsigned(Assembly a) => a.GetName().GetPublicKeyToken()?.Length == 0;

        public static bool HasRandomizedSuffix(string name) {
            return Regex.IsMatch(name, @"_[a-z0-9]{8}\.[a-z0-9]{3}$", RegexOptions.IgnoreCase);
        }

        public static bool HasNullCompanyAttribute(Assembly a)
        {
            string company = a.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
            return string.IsNullOrEmpty(company);
        }

        public static List<Assembly> GetPluginAssemblies()
        {
            List<Assembly> assemblies = new List<Assembly>();
            var assms = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assm in assms)
            {
              if(HasNullCompanyAttribute(assm) && HasRandomizedSuffix(assm.GetName().Name)){
                    assemblies.Add(assm);
                }
            }
            return assemblies;
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
        public static bool IsValidPlugin(string dllPath)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                return false;

            try
            {                
                Assembly assembly = Assembly.LoadFile(dllPath);
                Type pluginType = assembly
                    .GetTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
                return pluginType != null;
            }
            catch (Exception ex)
            {               
                Console.WriteLine($"Failed to load plugin from {dllPath}: {ex.Message}");
                return false;
            }
        }
    }
}
