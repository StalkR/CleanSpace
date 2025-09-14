using ProtoBuf;
using Shared.Events;
namespace CleanSpaceShared.Networking
{

    [ProtoContract]
    public class CleanSpaceChatterPacket : MessageBase
    {
        [ProtoMember(7)]        
        // The same type of session parameter attestation challenge as used in the hello packet.        
        public byte[] chatterParameters;

        // The payload of the chatter packet is either a dummy PluginValidationRequest packet or the actual
        // PluginValidationRequest with the real attestation challenge request. The client must know the length
        // of the chatter with the server in order to decode the correct message.

        [ProtoMember(8)]
        public byte[] chatterPayload;
    
        public override void ProcessClient<CleanSpaceChatterPacket>(CleanSpaceChatterPacket r)
        {
            Shared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceChatterReceived(this, r.SenderId, r.Target, r);
        }

       
        public override void ProcessServer<CleanSpaceChatterPacket>(CleanSpaceChatterPacket r)
        {
            Shared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceChatterReceived(this, r.SenderId, r.Target, r);
        }
    }

}
