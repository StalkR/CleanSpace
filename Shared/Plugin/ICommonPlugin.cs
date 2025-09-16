using CleanSpaceShared.Config;
using CleanSpaceShared.Logging;

namespace CleanSpaceShared.Plugin
{
    public interface ICommonPlugin
    {
        IPluginLogger Log { get; }
        IPluginConfig Config { get; }
        long Tick { get; }
    }
}