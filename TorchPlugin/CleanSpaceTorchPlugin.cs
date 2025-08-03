#define USE_HARMONY

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using CleanSpaceShared.Networking;
using HarmonyLib;
using NLog.LayoutRenderers;
using Sandbox.Game;
using Shared.Config;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;

using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
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
            Common.SetPlugin(this, gameVersion, StoragePath);

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
            
           
            initialized = true;
        }

        private void SessionStateChanged(ITorchSession session, TorchSessionState newstate)
        {
            switch (newstate)
            {
                case TorchSessionState.Loading:
                    // we can get away with registering communication here. this way, we dont risk missing an insta-join
                    PacketRegistry.Init(Log, PluginName);
                    RegisterPacketActions();
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

        private void RegisterPacketActions()
        {
            PluginValidationResponse.ProcessServerAction += PluginValidationResponse_ProcessServerAction;
            
        }

        private void PluginValidationResponse_ProcessServerAction(MessageBase obj)
        {

            Log.Info("Received a response from client. Checking expected response list...");
        }

        private void RegisterPackets()
        {

            PacketRegistry.Register<ProtoPacketData<PluginValidationRequest>>(
                110,
                () => new ProtoPacketData<PluginValidationRequest>(),
                (packet, sender) =>
                {
                    var message = packet.GetMessage();
                    message.ProcessServer();
                }
            );

            PacketRegistry.Register<ProtoPacketData<PluginValidationResponse>>(
               111,
               () => new ProtoPacketData<PluginValidationResponse>(),
               (packet, sender) =>
               {
                   var message = packet.GetMessage();
                   message.ProcessServer();
               }
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