using System.ComponentModel;

namespace CleanSpaceShared.Struct
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