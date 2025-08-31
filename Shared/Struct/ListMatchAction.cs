using System.ComponentModel;

namespace Shared.Struct
{
    public enum ListMatchAction
    {
        [Description("None")]
        None,
        [Description("Accept")]
        Accept,
        [Description("Deny")]
        Deny
    }
}