using System.ComponentModel;

namespace Shared.Struct
{
    public enum PluginListType
    {
        [Description("Whitelist")]
        Whitelist,
        [Description("Blacklist")]
        Blacklist 
    }
}