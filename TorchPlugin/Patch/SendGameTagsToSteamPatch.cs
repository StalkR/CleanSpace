using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Game;
using Shared.Plugin;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using VRage.Game;
using VRage.Library.Utils;
namespace TorchPlugin.Patch
{
    [HarmonyPatch(typeof(MyDedicatedServer), "SendGameTagsToSteam")]
    internal static class MyDedicatedServer_SendGameTagsToSteam_Patch
    {
        // Prefix runs before the original method
        static bool Prefix(MyDedicatedServer __instance)
        {
            if (MyGameService.GameServer != null)
            {
                StringBuilder stringBuilder = new StringBuilder();
                switch (__instance.GameMode)
                {
                    case MyGameModeEnum.Survival:
                        stringBuilder.Append($"S{(int)__instance.InventoryMultiplier}-{(int)__instance.BlocksInventoryMultiplier}-{(int)__instance.AssemblerMultiplier}-{(int)__instance.RefineryMultiplier}");
                        break;
                    case MyGameModeEnum.Creative:
                        stringBuilder.Append("C");
                        break;
                }

                string newGroupId = Common.CleanSpaceGroupID.ToString(); 
                string gameTags = string.Concat("groupId", newGroupId,
                                                " version", MyFinalBuildConstants.APP_VERSION,
                                                " datahash", DataIntegrityHelper.CallGetHashBase64(),
                                                " mods", __instance.ModCount,
                                                " gamemode", stringBuilder,
                                                " view", __instance.SyncDistance,
                                                " modsTotalSize", __instance.ModsTotalSize);

                MyGameService.GameServer.SetGameTags(gameTags);
                MyGameService.GameServer.SetGameData(MyFinalBuildConstants.APP_VERSION.ToString());
                MyGameService.GameServer.SetKeyValue("CONSOLE_COMPATIBLE", MyPlatformGameSettings.CONSOLE_COMPATIBLE ? "1" : "0");
                return false;
            }

            return true;
        }
    }


    public static class DataIntegrityHelper
    {
        private static MethodInfo _getHashBase64Method;

        static DataIntegrityHelper()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Sandbox.Game");
            if (asm == null)
                throw new InvalidOperationException("Sandbox.Game assembly not loaded.");

            // Find the internal type
            var type = asm.GetType("Sandbox.Engine.Utils.MyDataIntegrityChecker", throwOnError: true);

            // Find the static method
            _getHashBase64Method = type.GetMethod(
                "GetHashBase64",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (_getHashBase64Method == null)
                throw new InvalidOperationException("GetHashBase64 method not found on MyDataIntegrityChecker.");

        }

        public static string CallGetHashBase64()
        {
            return (string)_getHashBase64Method.Invoke(null, null);
        }
    }

}
