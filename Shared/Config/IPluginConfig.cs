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

        // Server Plugin
        bool DetectCodeChanges { get; set; }
        long TokenValidTimeSeconds { get; set; }

        ObservableCollection<PluginListEntry> AnalyzedPlugins { get; set; }
        ObservableCollection<string> SelectedPlugins   { get; set; }

        PluginListType PluginListType { get; set; }
        ListMatchAction ListMatchAction { get; set; }

    }
}