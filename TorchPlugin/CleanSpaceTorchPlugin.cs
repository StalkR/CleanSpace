#define USE_HARMONY

using CleanSpace.Patch;
using CleanSpaceShared.Networking;
using CleanSpaceShared.Scanner;
using HarmonyLib;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using Sandbox.Game.Multiplayer;
using Shared.Config;
using Shared.Events;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using Shared.Struct;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Server.Managers;
using Torch.Session;
using VRage.GameServices;
using VRage.Network;
using VRage.Replication;
using VRage.Utils;
using static Sandbox.ModAPI.MyModAPIHelper;
using MyMultiplayer = Sandbox.Engine.Multiplayer.MyMultiplayer;

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
        private static readonly string InstanceSecret = Shared.Plugin.Common.InstanceSecret;

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


            Log.Info($"Server group ID set to Clean Space information group ({Common.CleanSpaceGroupID}).");

            Torch.Managers.GetManager<InstanceManager>().InstanceLoaded += CleanSpaceTorchPlugin_InstanceLoaded;
           
            initialized = true;
            
        }

        private void CleanSpaceTorchPlugin_InstanceLoaded(Torch.Server.ViewModels.ConfigDedicatedViewModel obj)
        {
            MySandboxGame.ConfigDedicated.GroupID = Common.CleanSpaceGroupID;
            var m = Torch.Managers.GetManager<InstanceManager>();
            m.DedicatedConfig.GroupId = Common.CleanSpaceGroupID;
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
            ulong id = r.SenderId;
            List<string> analysis = r.PluginHashes.Select((element)=>AssemblyScanner.UnscrambleSecureFingerprint(element, Encoding.UTF8.GetBytes(r.Nonce))).ToList();
            Log.Info($"Hash list for client {id}: " + analysis.Join());
            ValidationResultData validationResult = ValidationManager.Validate(id, r.Nonce, analysis);
            Log.Info($"Validation status for {id}: " + validationResult.Code.ToString());

            if (validationResult.Code == ValidationResultCode.ALLOWED)
            {
                ConnectedClientDataMsg msg;
                passed.Add(id);
                if (heldConnections.ContainsKey(id) && heldConnections.TryRemove(id, out msg))
                {
                    Log.Info($"Sending join message to ID {id}");
                    ConnectedClientPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, ref msg, new EndpointId(id));
                }
                else
                {
                    Log.Warning($"Client ID {id} passed checks but we didn't have a held connection. Hmm...");
                }
            }
            else
            {             
                string newNonce = ValidationManager.RegisterNonceForPlayer(id, true);
                PluginValidationResult rs = new PluginValidationResult()
                {
                    Code = validationResult.Code,
                    Success = validationResult.Success,
                    PluginList = validationResult.PluginList,
                    SenderId = MyGameService.OnlineUserId,
                    TargetType = MessageTarget.Client,
                    Target = id,
                    UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Nonce = newNonce
                };
                Log.Info($"Sending result packet containing information about rejection to ID {id} and scheduling a ticket cancellation for connection request.");
                PacketRegistry.Send(rs, new EndpointId(id), newNonce);
                Task.Run(() => DelayedDisconnect(id));
            }
        }

        public async Task DelayedDisconnect(ulong id)
        {            
            await Task.Delay(1000);
            Log.Info($"Ticket cancelled for ID {id}.");
            ConnectedClientSendJoinPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, id, JoinResult.TicketCanceled);
        }

        public static async Task DelayedDisconnectWithGroupRedirect(ulong id)
        {
            await Task.Delay(10000);
            ConnectedClientDataMsg h;
            if(heldConnections.TryRemove(id, out h))
            {
                Common.Logger.Info($"No response from ID {id} [{h.Name ?? "?"}] (held connection still present). Attempting to direct to information group.");
                ConnectedClientSendJoinPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, id, JoinResult.NotInGroup);
            }           
        }

        private readonly static List<ulong> passed = new List<ulong>();
        private readonly static ConcurrentDictionary<ulong, ConnectedClientDataMsg> heldConnections = new ConcurrentDictionary<ulong, ConnectedClientDataMsg>();
        public static bool InitiateCleanSpaceCheck(ulong steamId, ConnectedClientDataMsg pausedMsg)
        {
            if (passed.Contains(steamId) || !Common.Config.Enabled)
                return true;
         
            Common.Logger.Info($"{PluginName}: Initiating clean space request for player {steamId}...");

            if (heldConnections.ContainsKey(steamId))
            {
                Logger.Debug($"{PluginName}: A held connection request for {steamId} already exists. Discarding it and using the new one.");
                heldConnections.Remove(steamId);
            }
            heldConnections[steamId] = pausedMsg;

            string nonce = ValidationManager.RegisterNonceForPlayer(steamId, true);
            var message = new PluginValidationRequest
            {
                SenderId = MyGameService.OnlineUserId,
                TargetType = MessageTarget.Client,
                Target = steamId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = nonce
            };
            PacketRegistry.Send(message, new EndpointId(steamId), nonce);
            Task.Run(() => DelayedDisconnectWithGroupRedirect(steamId));
            return false;
        }
        

        private void Torch_GameStateChanged(Sandbox.MySandboxGame game, TorchGameState newState)
        {
           if(newState == TorchGameState.Loaded)
            {
                MyMultiplayer.Static.ClientJoined += Static_ClientJoined;
                MyMultiplayer.Static.ClientLeft += Static_ClientLeft;
                
            }
        }

        private void Static_ClientLeft(ulong arg1, MyChatMemberStateChangeEnum arg2)
        {
            if (passed.Remove(arg1)){
                if(heldConnections.ContainsKey(arg1)) 
                    heldConnections.Remove(arg1);
                Log.Info($"Cleaning up for ID {arg1}");                
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
                    
                    PacketRegistry.InstallHandler(Log, PluginName);
                    RegisterPackets();
                    Init_Events();
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
              112,  () => new ProtoPacketData<PluginValidationResult>(), SecretPacketFactory<PluginValidationResult>.handler<ProtoPacketData<PluginValidationResult>>
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