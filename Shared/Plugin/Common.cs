using System;
using System.IO;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Util;
#if TORCH
#else
using CleanSpaceShared;
#endif
namespace Shared.Plugin
{
    public static class Common
    {
        public static ICommonPlugin Plugin { get; private set; }
        public static IPluginLogger Logger { get; private set; }
        public static IPluginConfig Config { get; private set; }

        public static int JITTEST_RESPONSE_WINDOW_SIZE = 256;

        public static string GameVersion;
        public static string DataDir;
        public static string PluginName;
        public static bool IsServer;
        private static string _instanceSecret;
        public static ulong CleanSpaceGroupID => 103582791475284354;

        public static Type[] CriticalTypes = new Type[]{
                        typeof(CleanSpaceShared.Scanner.AssemblyScanner),
                        typeof(Hasher.HasherRunner),
#if TORCH
#else
                        typeof(CleanSpaceClientPlugin)
#endif
        };

        public static string InstanceSecret = _instanceSecret != null ? _instanceSecret :  (_instanceSecret = TokenUtility.GenerateToken(DateTime.UtcNow.ToLongTimeString(), DateTime.UtcNow.AddDays(1), "secret"));
        public static void SetPlugin(ICommonPlugin plugin, string gameVersion, string storageDir, string pluginName, bool isServer, IPluginLogger logger)
        {
            Plugin = plugin;
            Logger = logger;
            Config = plugin.Config;
            PluginName = pluginName;
            IsServer = isServer;
            GameVersion = gameVersion;
            DataDir = Path.Combine(storageDir, "CleanSpace");
            PatchHelpers.Configure();
        }
    }
}