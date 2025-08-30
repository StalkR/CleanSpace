using System;
using System.IO;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;

namespace Shared.Plugin
{
    public static class Common
    {
        public static ICommonPlugin Plugin { get; private set; }
        public static IPluginLogger Logger { get; private set; }
        public static IPluginConfig Config { get; private set; }

        public static string GameVersion;
        public static string DataDir;
        public static string PluginName;
        public static bool IsServer;
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