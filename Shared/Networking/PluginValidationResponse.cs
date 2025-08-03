using ProtoBuf;

using System;
using System.Collections.Generic;
using System.Linq;
namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationResponse : MessageBase
    {
        [ProtoMember(2)]
        public List<string> PluginHashes; // SHA-256 or similar of loaded plugin assemblies
    }

}
