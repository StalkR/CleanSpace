using CleanSpace;
using CleanSpaceShared.Networking;
using CleanSpaceShared.Scanner;
using CleanSpaceShared.Settings;
using CleanSpaceShared.Settings.Layouts;
using EmptyKeys.UserInterface;
using HarmonyLib;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.Screens;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Events;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using Shared.Util;
using SpaceEngineers.Game.GUI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Input;
using VRage.FileSystem;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.GameServices;
using VRage.Network;
using VRage.Plugins;

namespace CleanSpaceShared
{

    // ReSharper disable once UnusedType.Global
    public class CleanSpaceClientPlugin : IPlugin, ICommonPlugin
    {
        public const string PluginName = "Clean Space";
        public static CleanSpaceClientPlugin Instance { get; private set; }
        private SettingsGenerator settingsGenerator;
        public long Tick { get; private set; }
        private static bool failed;

        public IPluginLogger Log => Logger;
        public static readonly IPluginLogger Logger = new PluginLogger(PluginName);

        public IPluginConfig Config => config?.Data;
        private PersistentConfig<PluginConfig> config;
        private static readonly string ConfigFileName = $"{PluginName}.cfg";

        public bool first_initialization = false;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {

#if DEBUG
            // Allow the debugger some time to connect once the plugin assembly is loaded
            Thread.Sleep(100);
#endif

            MyGuiScreenMainMenuBase.OnOpened = menuOpenEvent;
            Instance = this;
            Instance.settingsGenerator = new SettingsGenerator();

            Log.Info("Loading");

            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
            config = PersistentConfig<PluginConfig>.Load(Log, configPath);

            var gameVersion = MyFinalBuildConstants.APP_VERSION_STRING.ToString();

            Common.SetPlugin(this, gameVersion, MyFileSystem.UserDataPath, "Clean Space", false, Logger);

            if (!PatchHelpers.HarmonyPatchAll(Log, new Harmony(PluginName)))
            {
                failed = true;
                return;
            }            

            Log.Debug($"{PluginName} Loaded");
            init_events();
        }

        private void init_events()
        {
            Log.Info($"{PluginName}: Initializing events.");
            EventHub.ServerCleanSpaceRequested += EventHub_ServerCleanSpaceRequested;
            MyScreenManager.ScreenAdded += MyScreenManager_ScreenAdded; ;
            MySession.OnUnloaded += MySession_OnUnloaded;
        }

        private void MyScreenManager_ScreenAdded(MyGuiScreenBase obj)
        {
            if (obj is MyGuiScreenMainMenuBase)
            {
                Log.Info($"{PluginName}: Menu screen reached. Re-registering handlers.");
                PacketRegistry.InstallHandler(Log, PluginName);
                if (!first_initialization)
                {
                    first_initialization = true;                   
                    RegisterPackets();
                }
            }
        }

        private void MySession_OnUnloaded()
        {
          
        }

        private void MySession_OnLoading()
        {           

           
        }

        private void EventHub_ServerCleanSpaceRequested(object sender, CleanSpaceTargetedEventArgs e)
        {
            object[] args = e.Args;

            PluginValidationRequest r = (PluginValidationRequest)args[0];

            var steamId = Sandbox.Engine.Networking.MyGameService.OnlineUserId;

            if (e.Target == steamId)
            {
                string newToken = ValidationManager.RegisterNonceForPlayer(r.SenderId, true);
                var message = new PluginValidationResponse
                {
                    SenderId = steamId,
                    TargetType = MessageTarget.Server,
                    Target = r.SenderId,
                    UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Nonce = r.Nonce,
                    PluginHashes = AssemblyScanner.GetPluginAssemblies().Select<Assembly, string>((a) => AssemblyScanner.GetSecureAssemblyFingerprint(a, Encoding.UTF8.GetBytes(r.Nonce))).ToList()
                };

                // MiscUtil.PrintStateFor(PacketRegistry.Logger, r.SenderId);           
                PacketRegistry.Send(message, new EndpointId(r.SenderId), newToken, r.Nonce);
                PacketRegistry.Logger.Info($"{PacketRegistry.PluginName}: Sending a response to validation request.");
            }
            else
            {
                Log.Error($"{PluginName}: Received a clean space request... but it was for {r.Target} and not for me ({steamId})?");
            }
        }

        private void menuOpenEvent()
        {                          
          
            MyGuiScreenMainMenuBase.OnOpened = null;

        }


        private void LogMessage(MessageBase obj)
        {

        }
     
        private void RegisterPackets()
        {
            PacketRegistry.Register<PluginValidationRequest>(
                110,                 
                () => new ProtoPacketData<PluginValidationRequest>()
            );

            PacketRegistry.Register<PluginValidationResponse>(
               111,
               () => new ProtoPacketData<PluginValidationResponse>(), SecretPacketFactory<PluginValidationResponse>.handler<ProtoPacketData<PluginValidationResponse>>
           );

            PacketRegistry.Register<PluginValidationResult>(
              112,
              () => new ProtoPacketData<PluginValidationResult>(), SecretPacketFactory<PluginValidationResult>.handler<ProtoPacketData<PluginValidationResult>>
            );
        }

        public void Dispose()
        {
            try
            {
               
                // TODO: Save state and close resources here, called when the game exists (not guaranteed!)
                // IMPORTANT: Do NOT call harmony.UnpatchAll() here! I t may break other plugins.
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Dispose failed");
            }

            Instance = null;
        }

        public void Update()
        {
            if (failed)
                return;

            try
            {
                CustomUpdate();
                Tick++;
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Update failed");
                failed = true;
            }
        }

        private void CustomUpdate()
        {
            // TODO: Put your update code here. It is called on every simulation frame!
            PatchHelpers.PatchUpdates();
        }

        // ReSharper disable once UnusedMember.Global
        public void OpenConfigDialog()
        {
            Instance.settingsGenerator.SetLayout<Simple>();
            MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
        }
    }
}