using CleanSpace;
using ProtoBuf;
using Shared.Events;
using Shared.Struct;
using System.Collections.Generic;
namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationResponse : MessageBase
    {
        [ProtoMember(7)]
        public List<string> PluginHashes;

        public override void ProcessClient<PluginValidationResponse>(PluginValidationResponse r)
        {
            Shared.Plugin.Common.Logger.Info("Test");
        }

        public override void ProcessServer<PluginValidationResponse>(PluginValidationResponse r)
        {
            Shared.Plugin.Common.Logger.Info("Test");
            string token = r.Nonce;
            if (token == null)
            {
                Shared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, but the server did not provide a token for a response!");
                return;
            }
            if (r == null)
            {
                Shared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, But the packet was null when delivered to handler.");
                return;
            }

            var steamId = Sandbox.Engine.Networking.MyGameService.OnlineUserId;     
            Shared.Plugin.Common.Logger.Info($"Client ID {r.SenderId} responded with hash list. Processing...");
            EventHub.OnClientCleanSpaceResponded(this, r.SenderId, r);
        }
    }

}
