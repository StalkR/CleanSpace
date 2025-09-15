using CleanSpace;
using CleanSpaceShared.Scanner;
using Shared.Events;
using Shared.Plugin;
using Shared.Struct;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;

namespace TorchPlugin
{
    class CleanSpaceAssemblyManager
    {
        public static CleanSpaceAssemblyManager Instance { get; private set; }
        public static string torch_dir;
        public static string cs_data_storage_dir;
        public static string cs_assembly_storage_dir;
        public static string cs_adv_log_dir;
        public CleanSpaceAssemblyManager(string _torch_dir) {

            torch_dir = _torch_dir;
            cs_data_storage_dir = Path.Combine(_torch_dir, "CleanSpace");
            cs_assembly_storage_dir = Path.Combine(cs_data_storage_dir, "AssemblyStore");
            cs_adv_log_dir = Path.Combine(cs_data_storage_dir, "AdvancedLogs");
            InitializeDirectories();

            CleanSpaceTorchPlugin.Instance.Config.AnalyzedPlugins.CollectionChanged += AnalyzedPlugins_CollectionChanged;
            Instance = this;
        }

        private void AnalyzedPlugins_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    var p = item as PluginListEntry;
                    if (File.Exists(p.Location))
                    {
                        File.Delete(p.Location);
                    }
                }
            }
        }

        private void InitializeDirectories()
        {
            try
            {
                Directory.CreateDirectory(cs_data_storage_dir);
                Directory.CreateDirectory(cs_assembly_storage_dir);
                Directory.CreateDirectory(cs_adv_log_dir);
            }
            catch (Exception e){
                Common.Logger.Error("Clean Space data directories not initialized: " + e.Message);
                return;
            }         
        }

        private void Update_From_Store()
        {
            var enumeration = Directory.EnumerateFiles(cs_assembly_storage_dir);
            foreach (var file in enumeration){


            }
        }

        internal void Init_Events()
        {
            EventHub.CleanSpaceServerScannedPlugin += EventHub_CleanSpaceServerScannedPlugin;
        }

        private void EventHub_CleanSpaceServerScannedPlugin(object sender, CleanSpaceEventArgs e)
        {
            string file = (string)e.Args[0];
            AddPluginFromFile(file, e.Args[1] as Assembly);
        }

        private void AddPluginFromFile(string file, Assembly _a = null)
        {

            if (!File.Exists(file))
                return;

            string newName = Path.GetFileName(file);
            if (!Path.GetDirectoryName(file).Equals(cs_assembly_storage_dir))
            {
                newName = Guid.NewGuid().ToString() + "_" + newName;
            }

            String managerAssemblyLocation = Path.Combine(cs_assembly_storage_dir, newName);

            File.Copy(file, managerAssemblyLocation, true);
            Common.Logger.Info($"{Path.GetFileName(file)} copied to {managerAssemblyLocation} from {Path.GetDirectoryName(file)}");

            Assembly a = _a != null ? _a : Assembly.LoadFile(managerAssemblyLocation);
            String assemblyHash = AssemblyScanner.GetAssemblyFingerprint(a);
            String version = a.GetName().Version.ToString();
            String name = a.GetName().Name;
            PluginListEntry newEntry = new PluginListEntry()
            {
                AssemblyName = name,
                Hash = assemblyHash,
                LastHashed = DateTime.Now,
                Version = version,
                Name = name,
                Location = managerAssemblyLocation
            };

            Common.Logger.Info("Added: " + newEntry.ToString());
            var original = CleanSpaceTorchPlugin.Instance.Config.AnalyzedPlugins;
            var newList = new ObservableCollection<PluginListEntry>(original) { newEntry };
            CleanSpaceTorchPlugin.Instance.Config.AnalyzedPlugins = newList;
        }
        
    }
}
