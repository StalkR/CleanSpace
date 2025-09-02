#if !TORCH

using Shared.Struct;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System;
using System.Runtime.CompilerServices;

namespace Shared.Config
{
    public class PluginConfig : IPluginConfig
    {
        public event PropertyChangedEventHandler PropertyChanged;       

        private void SetValue<T>(ref T field, T value, [CallerMemberName] string propName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            
            OnPropertyChanged(propName);
        }

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChangedEventHandler propertyChanged = PropertyChanged;
            if (propertyChanged == null)
                return;

            propertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private long tokenValidTime;
        private bool enabled = true;
        private bool detectCodeChanges = true;
        private ObservableCollection<PluginListEntry> analyzedPlugins = new ObservableCollection<PluginListEntry>();
        private ObservableCollection<string> selectedPlugins = new ObservableCollection<string>();
        PluginListType pluginListType = PluginListType.Whitelist;
        ListMatchAction listMatchAction = ListMatchAction.Deny;

        public bool Enabled
        {
            get => enabled;
            set => SetValue(ref enabled, value);
        }

        public bool DetectCodeChanges
        {
            get => detectCodeChanges;
            set => SetValue(ref detectCodeChanges, value);
        }

        public long TokenValidTimeSeconds
        { 
            get => tokenValidTime; 
            set => SetValue(ref tokenValidTime, value); 
        }

        public ObservableCollection<PluginListEntry> AnalyzedPlugins
        {
            get => analyzedPlugins;
            set => SetValue(ref  analyzedPlugins, value);
        }

        public ObservableCollection<string> SelectedPlugins
        {
            get => selectedPlugins;
            set => SetValue(ref selectedPlugins, value);
        }

        public PluginListType PluginListType 
        { 
            get => pluginListType;
            set => SetValue(ref pluginListType, value);
        }

        public ListMatchAction ListMatchAction 
        { 
            get => listMatchAction;
            set => SetValue(ref listMatchAction, value);
        }

    }
}

#endif