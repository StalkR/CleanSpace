using System.ComponentModel;

namespace CleanSpaceShared.Struct
{
    public enum PluginListType
    {
        [Description("Whitelist")]
        Whitelist,
        [Description("Blacklist")]
        Blacklist 
    }
}