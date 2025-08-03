using Shared.Struct;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Shared.Config
{
    public interface IPluginConfig : INotifyPropertyChanged
    {
        bool Enabled { get; set; }
        bool DetectCodeChanges { get; set; }
        string Secret { get; set; }
        long TokenValidTimeTicks { get; set; }


    // Server Plugin
        ObservableCollection<PluginListEntry> AnalyzedPlugins { get; set; }
        ObservableCollection<string> SelectedPlugins   { get; set; }

        PluginListType PluginListType { get; set; }
        ListMatchAction ListMatchAction { get; set; }

    }
}