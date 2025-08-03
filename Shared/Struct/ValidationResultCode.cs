using System.Collections.Generic;

namespace Shared.Struct
{

    public enum ValidationResultCode
    {
        ALLOWED = 1,
        VALID_TOKEN = 0,
        INVALID_TOKEN = -1,
        EXPIRED_TOKEN = -2,
        UNEXPECTED_TOKEN = -3,
        MALFORMED_TOKEN = -4,
        REJECTED_MATCH = -5,   
        REJECTED_CLEANSPACE_HASH = -6
    }
    public struct ValidationResultData
    {
        public bool Success;
        public ValidationResultCode Code;
        public List<string> PluginList;
    }

}