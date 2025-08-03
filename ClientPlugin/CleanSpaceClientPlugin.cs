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
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
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
        public const string Name = "Clean Space";
        public static CleanSpaceClientPlugin Instance { get; private set; }
        private SettingsGenerator settingsGenerator;
        public long Tick { get; private set; }
        private static bool failed;

        public IPluginLogger Log => Logger;
        public static readonly IPluginLogger Logger = new PluginLogger(Name);

        public IPluginConfig Config => config?.Data;
        private PersistentConfig<PluginConfig> config;
        private static readonly string ConfigFileName = $"{Name}.cfg";

        public bool assembly_list_initialized = false;
        public List<Assembly> detectedPluginAssemblies;


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {

            detectedPluginAssemblies = new List<Assembly> { };
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
            Common.SetPlugin(this, gameVersion, MyFileSystem.UserDataPath);

            if (!PatchHelpers.HarmonyPatchAll(Log, new Harmony(Name)))
            {
                failed = true;
                return;
            }            
            Log.Debug($"{Name} Loaded");
        }

        private void menuOpenEvent()
        {
            
            Log.Debug($"{Name} Performing Scan...");
            detectedPluginAssemblies = AssemblyScanner.GetPluginAssemblies();
            Log.Debug("Detected plugin assemblies:");
            foreach (var assm in detectedPluginAssemblies) 
                Log.Debug(assm.FullName);
              
            assembly_list_initialized = true;

            PacketRegistry.Init(Log, Name);
            SetPacketActions();
            RegisterPackets();
            MyGuiScreenMainMenuBase.OnOpened = null;

        }

        private void SetPacketActions()
        {
            PluginValidationRequest.ProcessClientAction += PluginValidationRequest_ProcessAction;
            PluginValidationRequest.ProcessClientAction += LogMessage;
        }

        private void LogMessage(MessageBase obj)
        {

        }
        private void PluginValidationRequest_ProcessAction(MessageBase obj)
        {
            PluginValidationRequest r = (PluginValidationRequest)obj;

            string token = r.Nonce;
            if (token == null)
            {
                Log.Error($"{Name}: Received a validation request from the server, but the server did not provide a token!");
                return;
            }
            string myHash = AssemblyScanner.GetAssemblyFingerprint(AssemblyScanner.GetOwnAssembly());
            List<Assembly> pluginList = AssemblyScanner.GetPluginAssemblies();           
            List<String> pluginHashList = new List<String>();
            pluginList.ForEach((assm) => pluginHashList.Add(AssemblyScanner.GetAssemblyFingerprint(assm)));
            
            var steamId = MyMultiplayer.Static.GetOwner();            
            var message = new PluginValidationResponse
            {
                SenderId = steamId,
                TargetType = MessageTarget.Server,
                Target = obj.SenderId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = token,
                PluginHashes = pluginHashList,
                CleanSpaceHash = myHash
            };

            PacketRegistry.Send(message, new EndpointId(steamId), MyP2PMessageEnum.Reliable);
            Log.Error($"{Name}: Sending a response to validation request.");

        }

        private void RegisterPackets()
        {
            PacketRegistry.Register<ProtoPacketData<PluginValidationRequest>>(
                110, 
                
                () => new ProtoPacketData<PluginValidationRequest>(),
                (packet, sender) =>
                {
                    PluginValidationRequest receivedPacked = packet.GetMessage();
                    receivedPacked.ProcessClient();
                }

            );

            PacketRegistry.Register<ProtoPacketData<PluginValidationResponse>>(
               111,
               () => new ProtoPacketData<PluginValidationResponse>(),
               (packet, sender) =>
               {
                   PluginValidationResponse message = packet.GetMessage();
                   message.ProcessServer();
               }
           );
        }

        public void Dispose()
        {
            try
            {
               
                // TODO: Save state and close resources here, called when the game exists (not guaranteed!)
                // IMPORTANT: Do NOT call harmony.UnpatchAll() here! It may break other plugins.
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

        internal void SendPluginValidationResponse(string nonce)
        {
            List<string> hashes = new List<string>();
            detectedPluginAssemblies.ForEach((a) => hashes.Add(AssemblyScanner.GetAssemblyFingerprint(a)));
            Log.Info($"{Name}: Responding to request for plugin list");
            //Communication.SendMessageToServer(new PluginValidationResponse() { Nonce = nonce, PluginHashes = hashes });
            
        }
    }
}