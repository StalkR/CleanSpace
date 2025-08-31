using ProtoBuf;
using Shared.Events;
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
            Shared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received something that I should not have.");
            throw new System.SystemException($"{PacketRegistry.PluginName} encountered a critical issue it could not recover from. Please contact a developer with logs.");
        }

        public override void ProcessServer<PluginValidationResponse>(PluginValidationResponse r)
        {
            string token = r.Nonce;
            if (token == null){
                Shared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, but the server did not provide a token for a response!");
                return;
            }

            var steamId = Sandbox.Engine.Networking.MyGameService.OnlineUserId;     
            Shared.Plugin.Common.Logger.Info($"Client ID {r.SenderId} responded with hash list. Processing...");
            EventHub.OnClientCleanSpaceResponded(this, r.SenderId, r);
        }
    }

}
