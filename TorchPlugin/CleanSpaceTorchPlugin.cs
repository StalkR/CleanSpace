#define USE_HARMONY

using CleanSpace.Patch;
using CleanSpaceShared.Networking;
using HarmonyLib;
using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Game;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Shared.Config;
using Shared.Events;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using Shared.Struct;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
using Torch.Utils;
using VRage.GameServices;
using VRage.Network;
using VRage.Utils;

namespace CleanSpace
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CleanSpaceTorchPlugin : TorchPluginBase, IWpfPlugin, ICommonPlugin
    {

        public const string PluginName = "Clean Space";
        public static CleanSpaceTorchPlugin Instance { get; private set; }

        public long Tick { get; private set; }

        public IPluginLogger Log => Logger;
        private static readonly IPluginLogger Logger = new PluginLogger(PluginName);
        

        public IPluginConfig Config => config?.Data;
        private PersistentConfig<ViewModelConfig> config;
        private static readonly string ConfigFileName = $"{PluginName}.cfg";

        // ReSharper disable once UnusedMember.Global
        public UserControl GetControl() => control ?? (control = new ConfigView());
        private ConfigView control;

        private TorchSessionManager sessionManager;

        private bool initialized;
        private bool failed;

        // ReSharper disable once UnusedMember.Local
        private readonly Commands commands = new Commands();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            ConfigView.Log = Log;
            ValidationManager.Log = Log;
#if DEBUG
            // Allow the debugger some time to connect once the plugin assembly is loaded
            Thread.Sleep(100);
#endif
           
            Instance = this;
            Log.Info("Init");

            var configPath = Path.Combine(StoragePath, ConfigFileName);
            config = PersistentConfig<ViewModelConfig>.Load(Log, configPath);
            
            var gameVersionNumber = MyPerGameSettings.BasicGameInfo.GameVersion ?? 0;
            var gameVersion = new StringBuilder(MyBuildNumbers.ConvertBuildNumberFromIntToString(gameVersionNumber)).ToString();
            Common.SetPlugin(this, gameVersion, StoragePath, "Clean Space", true, Logger);

            var harm = new Harmony(Name);
            if (!PatchHelpers.HarmonyPatchAll(Log, harm))
            {
                failed = true;
                return;
            }
            else
            {
                Log.Info("Patches applied.");
            }
            
            sessionManager = torch.Managers.GetManager<TorchSessionManager>();
            sessionManager.SessionStateChanged += SessionStateChanged;
            torch.GameStateChanged += Torch_GameStateChanged;

            initialized = true;
            Init_Events();
        }

        private void Init_Events()
        {
            EventHub.ClientCleanSpaceResponded += EventHub_ClientCleanSpaceResponded;
        }

        private void EventHub_ClientCleanSpaceResponded(object sender, CleanSpaceTargetedEventArgs e)
        {
            object[] args = e.Args;
            if (args.Length == 0)
            {
                Log.Error("ClientCleanSpaceResponded triggered but CleanSpaceTargetedEventArgs had no Args.");
                return;
            }
            if (!(args[0] is PluginValidationResponse))
            {
                Log.Error("ClientCleanSpaceResponded triggered but response was not a response.");
                return;
            }

            PluginValidationResponse r = (PluginValidationResponse)args[0];
            string nonce = r.Nonce;
            ulong id = r.SenderId;
            List<string> analysis = r.PluginHashes;
            Log.Info($"Hash list for client {id}: " + analysis.Join());
            ValidationResultData validationResult = ValidationManager.Validate(id, r.Nonce, analysis);
            Log.Info($"Validation status for {id}: " + validationResult.Code.ToString());

            switch (validationResult.Code)
            {
                case ValidationResultCode.ALLOWED:
                        passed.Add(id);
                        //ConnectedClientPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, id, JoinResult.OK);
                    break;
                
                case ValidationResultCode.REJECTED_CLEANSPACE_HASH:

                    break;

                case ValidationResultCode.EXPIRED_TOKEN:
                    
                    break;
            }
        }

        private static List<ulong> pending = new List<ulong>();
        private static List<ulong> passed = new List<ulong>();
        public static bool InitiateCleanSpaceCheck(ulong steamId)
        {
            if (passed.Contains(steamId))
            {
                // TODO: Send a message congratulating the user for not cheating.
                return true;
            }

            MyLog.Default.WriteLineAndConsole($"{CleanSpaceTorchPlugin.PluginName}: Initiating clean space request for player {steamId} .");
            string nonce = ValidationManager.RegisterNonceForPlayer(steamId);
            var message = new PluginValidationRequest
            {
                SenderId = MyGameService.OnlineUserId,
                TargetType = MessageTarget.Client,
                Target = steamId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = nonce
            };
            PacketRegistry.Send(message, new EndpointId(steamId), nonce);
            return false;
        }
        
 
        protected struct MyConnectedClientData
        {
            public string Name;

            public string PlatformName;

            public bool IsAdmin;

            public bool IsProfiling;

            public string ServiceName;
        }
       

        private void Torch_GameStateChanged(Sandbox.MySandboxGame game, TorchGameState newState)
        {
           if(newState == TorchGameState.Loaded)
            {
                MyMultiplayer.Static.ClientJoined += Static_ClientJoined;
            }
        }

        public void printStateFor(ulong steamid)
        {
            MyP2PSessionState state = new MyP2PSessionState();
            if (MyGameService.Peer2Peer.GetSessionState(steamid, ref state))
            {
                Log.Info($"Connecting: {state.Connecting}");
                Log.Info($"ConnectionActive: {state.ConnectionActive}");
                Log.Info($"RemoteIP: {state.RemoteIP}");
                Log.Info($"RemotePort: {state.RemotePort}");
            }
        }
        private void Static_ClientJoined(ulong steamId, string username)
        {
            Log.Info($"Joined: {steamId}:{username}");
            //InitiateCleanSpaceCheck(steamId);
          
          
        }

        private void SessionStateChanged(ITorchSession session, TorchSessionState newstate)
        {
            switch (newstate)
            {
                case TorchSessionState.Loading:
                    PacketRegistry.Init(Log, PluginName);
                    RegisterPackets();

                    break;

                case TorchSessionState.Loaded:
                  
                   
                    break;
             
                case TorchSessionState.Unloading:

                    break;


                case TorchSessionState.Unloaded:
                    break;
            }
        }

        public static void RejectConnection(ulong steamId, string reason)
        {
            MyLog.Default.WriteLineAndConsole($"{CleanSpaceTorchPlugin.PluginName}: Player {steamId} was rejected by clean space: {reason}");

            var server = MyMultiplayer.Static as MyDedicatedServerBase;
            var sendJoinResult = AccessTools.Method(server.GetType(), "SendJoinResult");
            sendJoinResult?.Invoke(server, new object[] { steamId, JoinResult.TicketCanceled, 0UL });

        }

        private void RegisterPackets()
        {

            PacketRegistry.Register<PluginValidationRequest>(
              110, () => new ProtoPacketData<PluginValidationRequest>(), SecretPacketFactory<PluginValidationRequest>.handler<ProtoPacketData<PluginValidationRequest>>
            );

            PacketRegistry.Register<PluginValidationResponse>(
               111, () => new ProtoPacketData<PluginValidationResponse>(),  SecretPacketFactory<PluginValidationResponse>.handler<ProtoPacketData<PluginValidationResponse>>
            );
          
            PacketRegistry.Register<PluginValidationResult>(
              112,
              () => new ProtoPacketData<PluginValidationResult>(), SecretPacketFactory<PluginValidationResult>.handler<ProtoPacketData<PluginValidationResult>>
            );

        }

        public override void Dispose()
        {
            if (initialized)
            {
                Log.Debug("Disposing");

                sessionManager.SessionStateChanged -= SessionStateChanged;
                sessionManager = null;
              

                Log.Debug("Disposed");
            }

            Instance = null;

            base.Dispose();
        }

        public override void Update()
        {
            if (failed)
                return;

            try
            {
                CustomUpdate();
                Tick++;
            }
            catch (Exception e)
            {
                Log.Critical(e, "Update failed");
                failed = true;
            }
        }

        private void CustomUpdate()
        {
            // TODO: Put your update processing here. It is called on every simulation frame!            no -F
            PatchHelpers.PatchUpdates();
        }
    }
}