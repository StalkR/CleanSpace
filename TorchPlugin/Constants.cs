using System;
namespace TorchPlugin
{
    internal static class Constants
    {
        public static TimeSpan CFG_HELLO_TASK_RETRY_DELAY = TimeSpan.FromSeconds(5);
        public static int CFG_HELLO_TASK_RETRIES_MAX = 2;
    }
}
