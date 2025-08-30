using CleanSpace;
using CleanSpaceShared.Scanner;
using ProtoBuf;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Shared.Events;
using Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using VRage.GameServices;
using VRage.Network;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationRequest : MessageBase
    {
                
        public override void ProcessClient<PluginValidationRequest>(PluginValidationRequest r)
        {          
            string token = r.Nonce;            
            if (token == null)
            {                
                PacketRegistry.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, but the server did not provide a token for a response!");
                return;
            }
            EventHub.OnServerCleanSpaceRequested(this, r.Target, r);
        }

        public override void ProcessServer<PluginValidationRequest>(PluginValidationRequest r)
        {
            PacketRegistry.Logger.Error($"{PacketRegistry.PluginName}: Received something that I shouldn'tve.");
            throw new System.SystemException("Something is wrong. Closing down");
        }
    }

}
