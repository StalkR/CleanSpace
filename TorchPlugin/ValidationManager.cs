using CleanSpaceShared;
using CleanSpaceShared.Networking;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Network;
using VRage.Utils;

namespace CleanSpace
{
    public static class ValidationManager
    {
        private static Dictionary<ulong, string> expectedNonces = new Dictionary<ulong, string>();

        public static void RegisterNonceForPlayer(ulong steamId, string nonce)
        {
            expectedNonces[steamId] = nonce;
        }

        public static bool ValidatePluginHashes(ulong steamId, string receivedNonce, List<string> pluginHashes)
        {
            if (!expectedNonces.TryGetValue(steamId, out var validNonce))
                return false;

            expectedNonces.Remove(steamId);

            if (receivedNonce != validNonce)
                return false;

            return pluginHashes.All(hash => Whitelist.Contains(hash));
        }

        public static void RejectConnection(ulong steamId, string reason)
        {
            MyLog.Default.WriteLineAndConsole($"{TorchDetectorPlugin.PluginName}: Player {steamId} was rejected by clean space: {reason}");

            var server = MyMultiplayer.Static as MyDedicatedServerBase;
            var sendJoinResult = AccessTools.Method(server.GetType(), "SendJoinResult");
            sendJoinResult?.Invoke(server, new object[] { steamId, JoinResult.TicketInvalid, 0UL });

            

            // Optional: also kick player here depending on server API
        }

        private static readonly HashSet<string> Whitelist = new HashSet<string>()
    {
        "ABC123HASHEDPLUGIN1",
        "DEF456HASHEDPLUGIN2"
    };
    }
}
