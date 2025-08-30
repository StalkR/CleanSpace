

using Sandbox.Engine.Networking;
using Shared.Logging;
using System;
using VRage.GameServices;

namespace Shared.Util{

    public class MiscUtil
    {
        public static char GetRandomChar(string charSet)
        {
            Random random = new Random();
            int randomIndex = random.Next(0, charSet.Length);
            return charSet[randomIndex];
        }
        public static void PrintStateFor(IPluginLogger Log, ulong steamid)
        {
            MyP2PSessionState state = new MyP2PSessionState();
            if (MyGameService.Peer2Peer.GetSessionState(steamid, ref state))
            {
                Log.Info($"Connecting: {state.Connecting}");
                Log.Info($"ConnectionActive: {state.ConnectionActive}");
                Log.Info($"RemoteIP: {state.RemoteIP}");
                Log.Info($"RemotePort: {state.RemotePort}");
            }
        }

        internal static string GetRandomChars(string v1, int v2)
        {
            var arr = new char[v2];
            for (int i = 0; i < v2; i++)
            {
                arr[i] = GetRandomChar(v1);
            }
            return arr.ToString();
        }
    }
}