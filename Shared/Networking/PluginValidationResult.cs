using ProtoBuf;
using CleanSpaceShared.Events;
using CleanSpaceShared.Struct;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationResult : MessageBase
    {
        [ProtoMember(7)]
        public bool Success;

        [ProtoMember(8)]
        public ValidationResultCode Code;

        [ProtoMember(9)]
        public List<string> PluginList;

        public override void ProcessClient<PluginValidationResult>(PluginValidationResult r)
        {
            EventHub.OnServerCleanSpaceFinalized(this, r.Target, r);
        }

        public override void ProcessServer<PluginValidationResult>(PluginValidationResult r)
        {
            CleanSpaceShared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received something that I should not have.");
            throw new System.SystemException($"{PacketRegistry.PluginName} encountered a critical issue it could not recover from. Please contact a developer with logs.");
        }
    }
        

}
