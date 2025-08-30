using CleanSpaceShared;
using CleanSpaceShared.Networking;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Reflection;
using System.Windows.Documents;
using Torch;
using VRage;
using VRage.GameServices;
using VRage.Network;
using VRage.Utils;

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
            return CleanSpaceTorchPlugin.InitiateCleanSpaceCheck(steamId, msg);
        }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [HarmonyReversePatch]
        public static void CallPrivateMethod(MyDedicatedServerBase instance, ref ConnectedClientDataMsg msg, EndpointId playerId)
        {
            // This method body will be replaced by the original private method's IL
            throw new NotImplementedException("This method should be replaced by Harmony!");
        }
    }

    [HarmonyPatch(typeof(MyDedicatedServerBase))]
    public class ConnectedClientSendJoinPatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static MethodBase TargetMethod()
        {
            return typeof(MyDedicatedServerBase).GetMethod("SendJoinResult", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [HarmonyReversePatch]
        public static void CallPrivateMethod(MyDedicatedServerBase instance, ulong sendTo, JoinResult joinResult, ulong adminID = 0uL)
        {
            // This method body will be replaced by the original private method's IL
            throw new NotImplementedException("This method should be replaced by Harmony!");
        }
    }

    [HarmonyPatch(typeof(MyDedicatedServerBase))]
    public class IsUniqueMemberNamePatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static MethodBase TargetMethod()
        {
            return typeof(MyDedicatedServerBase).GetMethod("IsUniqueMemberName", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [HarmonyReversePatch]
        public static bool CallPrivateMethod(MyDedicatedServerBase instance, string name)
        {
            // This method body will be replaced by the original private method's IL
            throw new NotImplementedException("This method should be replaced by Harmony!");
        }
    }

    [HarmonyPatch(typeof(MyMultiplayerBase))]
    public class RaiseClientJoinedPatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static MethodBase TargetMethod()
        {
            return typeof(MyDedicatedServerBase).GetMethod("RaiseClientJoined", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [HarmonyReversePatch]
        public static void CallPrivateMethod(MyMultiplayerBase instance, ulong changedUser, string userName)
        {
            // This method body will be replaced by the original private method's IL
            throw new NotImplementedException("This method should be replaced by Harmony!");
        }
    }
}
