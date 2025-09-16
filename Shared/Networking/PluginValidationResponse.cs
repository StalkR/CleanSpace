using ProtoBuf;
using CleanSpaceShared.Events;
using System.Collections.Generic;
namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationResponse : MessageBase
    {
        [ProtoMember(7)]
        public List<string> PluginHashes;
        [ProtoMember(8)]
        public string attestationResponse;
        [ProtoMember(9)]
        public byte[] newSteamToken;

        public override void ProcessClient<PluginValidationResponse>(PluginValidationResponse r)
        {
            CleanSpaceShared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received something that I should not have.");
            throw new System.SystemException($"{PacketRegistry.PluginName} encountered a critical issue it could not recover from. Please contact a developer with logs.");
        }

        public override void ProcessServer<PluginValidationResponse>(PluginValidationResponse r)
        {
            string token = r.NonceS;
            if (token == null){
                CleanSpaceShared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, but the server did not provide a token for a response!");
                return;
            }

            EventHub.OnClientCleanSpaceResponded(this, r.SenderId, r);
        }
    }

}
