using System;
using System.Linq;
using System.Reflection;

namespace ClientPlugin
{
    internal static class ClientSessionParameterProviders
    {
        public static void RegisterProviders()
        {

            SessionParameterFactory.RegisterProvider(RequestType.MethodIL, (args) =>
            {
                MethodIdentifier m = ProtoUtil.Deserialize<MethodIdentifier>((byte[])args[0]);
                // Find the assembly that contains the type
                Type t = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType(m.FullName, throwOnError: false))
                    .FirstOrDefault(x => x != null);

                if (t == null)
                    throw new InvalidOperationException($"Type {m.FullName} not found in loaded assemblies");

                MethodBase method = t.GetMethod(
                    m.MethodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                );

                if (method == null)
                    throw new InvalidOperationException($"Method {m.MethodName} not found on type {m.FullName}");
                return ILAttester.GetMethodIlBytes(method);
            });

       /*     SessionParameterFactory.RegisterProvider(RequestType.TypeIL, (args) =>
            {
                MethodIdentifier m = ProtoUtil.Deserialize<MethodIdentifier>((byte[])args[0]);

                Type t = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType(m.FullName, throwOnError: false))
                    .FirstOrDefault(x => x != null);

                if (t == null)
                    throw new InvalidOperationException($"Type {m.FullName} not found in loaded assemblies");

                return ILAttester.GetTypeIlBytes(t);
            });*/

            Shared.Plugin.Common.Logger.Info($"{Shared.Plugin.Common.Logger}: Validation providers registered.");
        }
    }
}
