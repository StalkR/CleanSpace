using ProtoBuf;
using Shared.Struct;
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
        [ProtoMember(6)]
        public bool Success;

        [ProtoMember(7)]
        public ValidationResultCode Code;

        [ProtoMember(8)]
        public string Message;

        [ProtoMember(9)]
        public List<string> PluginList;

        public override void ProcessClient<PluginValidationResult>(PluginValidationResult r)
        {
           
        }

        public override void ProcessServer<PluginValidationResult>(PluginValidationResult r)
        {
           
        }
    }
        

    }
