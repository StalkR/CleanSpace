

using Sandbox.Engine.Networking;
using System;
using System.ComponentModel;

namespace Shared.Events
{
    public enum CleanSpaceEvent
    {
        CLIENT_CONNECTED = 10,
        SERVER_CS_REQUESTED = 100,
        CLIENT_CS_RESPONDED = 101,
        SERVER_CS_FINALIZED = 102
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
    public class EventHub
    {
        public static event EventHandler<CleanSpaceEventArgs> ClientConnected;
        public static event EventHandler<CleanSpaceTargetedEventArgs> ServerCleanSpaceRequested;
        public static event EventHandler<CleanSpaceTargetedEventArgs> ClientCleanSpaceResponded;
        public static event EventHandler<CleanSpaceTargetedEventArgs> ServerCleanSpaceFinalized;
        public static bool IsServer => Shared.Plugin.Common.IsServer;
        public static string PluginName => Shared.Plugin.Common.PluginName;
        public static ulong? MyID => MyGameService.OnlineUserId;

        public static void OnServerCleanSpaceRequested(object sender, ulong steamId, params object[] args)
            => ServerCleanSpaceRequested?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.SERVER_CS_REQUESTED, IsServer, steamId, MyID ?? 0, args));        

        public static void OnClientCleanSpaceResponded(object sender, ulong steamId, params object[] args)
            => ClientCleanSpaceResponded?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.CLIENT_CS_RESPONDED, IsServer, steamId, MyID ?? 0, args));       

        public static void OnServerCleanSpaceFinalized(object sender, ulong steamId, params object[] args)
            => ServerCleanSpaceFinalized?.Invoke(sender, new CleanSpaceTargetedEventArgs(CleanSpaceEvent.SERVER_CS_FINALIZED, IsServer, steamId, MyID ?? 0, args));
        
    }   
}