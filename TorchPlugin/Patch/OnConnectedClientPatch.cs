using CleanSpaceShared;
using CleanSpaceShared.Networking;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Documents;
using VRage;
using VRage.GameServices;
using VRage.Network;
using VRage.Utils;
using static Sandbox.Game.Replication.History.MySnapshotHistory;

namespace CleanSpace.Patch
{

    [HarmonyPatch]
    public static class ConnectedClientPatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static MethodBase TargetMethod()
        {
            var type = typeof(MyDedicatedServerBase);
            return AccessTools.Method(type, "OnConnectedClient");
        }

        // Prefix executes *before* the original method
        // Returning false = skip original
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static bool Prefix(ref ConnectedClientDataMsg msg, ulong steamId)
        {
            if (!InitiateCleanSpaceCheck(steamId, msg))
            {
                return false; // Cancel original OnConnectedClient; They failed, the fools.
            }
            return true;
        }

        private static List<ulong> pending = new List<ulong>();
        private static List<ulong> passed = new List<ulong>();
        private static bool InitiateCleanSpaceCheck(ulong steamId, ConnectedClientDataMsg msg)
        {
            if (passed.Contains(steamId))
            {
                return true;
            }

            MyLog.Default.WriteLineAndConsole($"{TorchDetectorPlugin.PluginName}: Initiating clean space request for player {steamId} .");
            string nonce = TimeHashUtility.GenerateToken(steamId.ToString()+"|"+TimeHashUtility.SharedSecret, TimeSpan.FromMilliseconds(TorchDetectorPlugin.Instance.Config.TokenValidTime));
            ValidationManager.RegisterNonceForPlayer(steamId, nonce);
            pending.Add(steamId);

            var message = new PluginValidationRequest
            {
                SenderId = MyMultiplayer.Static.ServerId,
                TargetType = MessageTarget.Client,
                Target = steamId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = nonce
            };
            
            PacketRegistry.Send(message, new EndpointId(steamId), MyP2PMessageEnum.Reliable);             


            // now we wait for a response, and while we do we block a successful join. The response is received and handled in the actual Messaging class, which calls back to this one.
            return false;
        }
    }
}
