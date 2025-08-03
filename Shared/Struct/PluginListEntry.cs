using System;
using System.Collections.Generic;
using System.Text;

namespace Shared.Struct
{
    [Serializable]
    public struct PluginListEntry
    {
        public bool IsSelected { get; set; }
        public string Name { get; set; }
        public string AssemblyName { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }
        public DateTime LastHashed { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is PluginListEntry))
                return false;

            PluginListEntry mys = (PluginListEntry)obj;
            return (Hash == mys.Hash);
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }
    }
}
