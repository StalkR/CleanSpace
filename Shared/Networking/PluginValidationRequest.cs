using ProtoBuf;
using CleanSpaceShared.Events;
using CleanSpaceShared.Util;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationRequest : MessageBase
    {
        [ProtoMember(7)]
        public byte[] attestationSignature;
        [ProtoMember(8)]
        public byte[] attestationChallenge;
        public override void ProcessClient<PluginValidationRequest>(PluginValidationRequest r)
        {          
            MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnServerCleanSpaceRequested(this, r.Target, r);
        }

        public override void ProcessServer<PluginValidationRequest>(PluginValidationRequest r)
        {
            CleanSpaceShared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received something that I should not have.");
            throw new System.SystemException($"{PacketRegistry.PluginName} encountered a critical issue it could not recover from. Please contact a developer with logs.");
        }
    }

}
