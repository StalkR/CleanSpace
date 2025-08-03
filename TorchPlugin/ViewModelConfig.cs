using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shared.Config;
using Shared.Struct;
using Torch;
using Torch.Views;

namespace CleanSpace
{
    [Serializable]
    public class ViewModelConfig : ViewModel, IPluginConfig
    {

        private long tokenValidTimeTicks = 2500;
        private bool enabled = true;
        private bool detectCodeChanges = true;

        private string secret = "secret";
               
        public long TokenValidTimeTicks { get => tokenValidTimeTicks; set => SetValue(ref tokenValidTimeTicks, value); }

        [Display(Order = 1, GroupName = "General", Name = "Enable plugin", Description = "Enable the plugin")]
        public bool Enabled
        {
            get => enabled;
            set => SetValue(ref enabled, value);
        }

        [Display(Order = 2, GroupName = "General", Name = "Detect code changes", Description = "Disable the plugin if any changes to the game code are detected before patching")]
        public bool DetectCodeChanges
        {
            get => detectCodeChanges;
            set => SetValue(ref detectCodeChanges, value);
        }


        [Display(Order = 1, GroupName = "Security", Name = "Secret Key", Description = "This secret key is used in generating validation responses. Make it unique.")] 
        public string Secret
        {
            get => secret;
            set => SetValue(ref secret, value);
        }
        ObservableCollection<PluginListEntry> analyzedPlugins = new ObservableCollection<PluginListEntry>();

        [Display(Order = 1, GroupName = "Plugins", Name = "Analyzed Plugins", Description = "This list displays the plugins that have been analyzed by Clean Space in this torch instance.")]
        public ObservableCollection<PluginListEntry> AnalyzedPlugins 
        {
            get => analyzedPlugins;              
            set => SetValue(ref analyzedPlugins, value);
        }

        ObservableCollection<string> selectedPlugins = new ObservableCollection<string>();
        public ObservableCollection<string> SelectedPlugins 
        {
            get => selectedPlugins;
            set => SetValue(ref selectedPlugins, value);
        }

        PluginListType pluginListType;
        public PluginListType PluginListType 
        { 
            get => pluginListType; 
            set => SetValue(ref pluginListType, value);
        }

        ListMatchAction listMatchAction;
        public ListMatchAction ListMatchAction 
        {
            get => listMatchAction; 
            set => SetValue(ref listMatchAction, value);
        }
    }
}