

using CleanSpaceShared.Networking;
using Sandbox.Engine.Networking;
using Sandbox.Game.Multiplayer;
using Shared.Events;
using Shared.Logging;
using System;
using System.Net;
using VRage.GameServices;

namespace Shared.Util{

    public class MiscUtil
    {
        public static void BasicReceivingPacketChecks<T>(T m) where T : MessageBase
        {
            if (m == null)
                throw new Exception("Null message passed to receiving checks.");
            if (m.Nonce == null)
                throw new Exception("Invalid message received with a null token.");
            if (m.SenderId == Sync.MyId)
                throw new Exception("Received a packet that I sent? That's not right.");
            if (m.SenderId <= 0 || m.Target <= 0)
                throw new Exception("SenderID or TargetID was corrupted on a received packet.");
        }
        public static ulong IpAddressToULong(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 addresses can be converted to ulong in this manner.", nameof(ipAddress));
            }

            byte[] bytes = ipAddress.GetAddressBytes();
            ulong result = ((ulong)bytes[0] << 24) |
                           ((ulong)bytes[1] << 16) |
                           ((ulong)bytes[2] << 8) |
                           ((ulong)bytes[3]);

            return result;
        }

        public static IPAddress TryGetPublicIPFromSteam()
        {
            MyP2PSessionState state = new MyP2PSessionState();
            if (MyGameService.Peer2Peer.GetSessionState(Sync.MyId, ref state))
            {
                return new IPAddress(state.RemoteIP);
            }
            return null;
        }

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

        internal static void ArgsChecks<T>(CleanSpaceTargetedEventArgs e, int v) where T : MessageBase
        {
            if (e == null) throw new Exception($"Args given were null. ");
            if (e.Args.Length == 0) throw new Exception($"Args given had a count of zero.");
            if (e.Args.Length < v) throw new Exception($"Args given with length {e.Args.Length} did meet the minimum length of length {v} where message data is expected.");
            if (e.Args[v-1] == null) throw new Exception($"Message data in args was null.");
            var eArgElement = e.Args[v-1];
            if (eArgElement as T == null) throw new Exception($"Object of type {eArgElement.GetType().Name} could not be casted to expected type {typeof(T).Name}.");            
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