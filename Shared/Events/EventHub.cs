using Sandbox.Engine.Networking;
using System;
using System.Reflection;
using VRage.Network;

namespace Shared.Events
{
    public enum CleanSpaceEvent
    {
        CLIENT_CONNECTION_STATE_CHANGED = 1,
        CLIENT_CONNECTED = 10,
        REGISTERED_NONCE = 40,
        REMOVED_NONCE = 41,
        CS_HELLO = 90,
        SERVER_CS_REQUESTED = 100,
        CLIENT_CS_RESPONDED = 101,
        SERVER_CS_FINALIZED = 102,
        SERVER_CS_ACCEPTED = 121,
        SERVER_CS_REJECTED = 122,
        CS_CHATTER = 123,
        SERVER_CS_SCANNNED_PLUGIN = 124,
    }
    public class CleanSpaceEventArgs : EventArgs
    {
        public object[] Args;
        public CleanSpaceEvent Id { get; }
        public bool IsServer { get; }
        public CleanSpaceEventArgs(CleanSpaceEvent Id, bool IsServer, params object[] args) {
            this.Id = Id;
            this.IsServer = IsServer;
            this.Args = args;
        }
    }

    public class CleanSpaceTargetedEventArgs : CleanSpaceEventArgs
    {
        public ulong Target;
        public ulong Source;
        public CleanSpaceTargetedEventArgs(CleanSpaceEvent Id, bool IsServer, ulong Target, ulong Source, params object[] args) : base(Id, IsServer, args)
        {
          this.Target = Target;
          this.Source = Source;
        }

    }

    public class CleanSpaceClientSessionEventArgs : CleanSpaceEventArgs
    {
        public ulong Source;
        public CleanSpaceClientSessionEventArgs(CleanSpaceEvent Id, bool IsServer, ulong Source, params object[] args) : base(Id, IsServer, args)
        {
            this.Source = Source;
        }

    }

    public class EventHub
    {
        public static event EventHandler<CleanSpaceClientSessionEventArgs> ServerConnectionAccepted;
        public static event EventHandler<CleanSpaceClientSessionEventArgs> ServerConnectionRejected;
        public static event EventHandler<CleanSpaceEventArgs> ClientConnected;
        public static event EventHandler<CleanSpaceEventArgs> NonceRegistered;
        public static event EventHandler<CleanSpaceEventArgs> ConnectionStateChanged;

        public static event EventHandler<CleanSpaceTargetedEventArgs> CleanSpaceHelloReceived;
        public static event EventHandler<CleanSpaceTargetedEventArgs> CleanSpaceChatterReceived;

        public static event EventHandler<CleanSpaceTargetedEventArgs> ServerCleanSpaceRequested;
        public static event EventHandler<CleanSpaceTargetedEventArgs> ClientCleanSpaceResponded;
        public static event EventHandler<CleanSpaceTargetedEventArgs> ServerCleanSpaceFinalized;

        public static event EventHandler<CleanSpaceEventArgs> CleanSpaceServerScannedPlugin;

        public static bool IsServer => Shared.Plugin.Common.IsServer;
        public static string PluginName => Shared.Plugin.Common.PluginName;
        public static ulong? MyID => MyGameService.OnlineUserId;


        public static void OnServerConnectionAccepted(object sender, ulong steamId, params object[] args)
          => ServerConnectionAccepted?.Invoke(sender, new CleanSpaceClientSessionEventArgs(CleanSpaceEvent.SERVER_CS_ACCEPTED, IsServer, steamId, args));
        public static void OnServerConnectionRejected(object sender, ulong steamId, params object[] args)
          => ServerConnectionRejected?.Invoke(sender, new CleanSpaceClientSessionEventArgs(CleanSpaceEvent.SERVER_CS_REJECTED, IsServer, steamId, args));

        public static void OnCleanSpaceHelloReceived(object sender, ulong source, ulong target, params object[] args)
          => CleanSpaceHelloReceived?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.CS_HELLO, IsServer, target, source, args));

        public static void OnServerCleanSpaceRequested(object sender, ulong steamId, params object[] args)
            => ServerCleanSpaceRequested?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.SERVER_CS_REQUESTED, IsServer, steamId, MyID ?? 0, args));        

        public static void OnClientCleanSpaceResponded(object sender, ulong steamId, params object[] args)
            => ClientCleanSpaceResponded?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.CLIENT_CS_RESPONDED, IsServer, steamId, MyID ?? 0, args));       

        public static void OnServerCleanSpaceFinalized(object sender, ulong steamId, params object[] args)
            => ServerCleanSpaceFinalized?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.SERVER_CS_FINALIZED, IsServer, steamId, MyID ?? 0, args));

        public static void OnNonceRegistered(object sender, ulong steamId, CleanSpace.PendingNonce e)
          => NonceRegistered?.Invoke(sender, new CleanSpaceEventArgs(CleanSpaceEvent.REGISTERED_NONCE, IsServer, steamId, e));

        public static void OnNonceRemoved(object sender, ulong steamId, CleanSpace.PendingNonce e)
        => NonceRegistered?.Invoke(sender, new CleanSpaceEventArgs(CleanSpaceEvent.REMOVED_NONCE, IsServer, steamId, e ));

        internal static void OnClientConnected(object sender, ConnectedClientDataMsg msg, ulong steamId)
          => ClientConnected?.Invoke(sender, new CleanSpaceEventArgs(CleanSpaceEvent.CLIENT_CONNECTED, IsServer, steamId, msg ));

        public static void OnConnectionStateChanged(object sender, ulong steamId, int new_state)
            => ConnectionStateChanged?.Invoke(sender, new CleanSpaceEventArgs(CleanSpaceEvent.CLIENT_CONNECTION_STATE_CHANGED, IsServer, steamId, new_state));

        public static void OnCleanSpaceChatterReceived(object sender, ulong source, ulong target, params object[] args)
         => CleanSpaceChatterReceived?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.CS_CHATTER, IsServer, target, source, args));

        public static void OnCleanSpaceServerScannedPlugin(object sender, string source, Assembly asm)
            => CleanSpaceServerScannedPlugin.Invoke(sender, new CleanSpaceEventArgs(CleanSpaceEvent.SERVER_CS_SCANNNED_PLUGIN, true, source, asm));
    }   

}