using System;
namespace CleanSpaceTorch
{
    public static class Constants
    {
        public static TimeSpan CFG_HELLO_TASK_RETRY_DELAY = TimeSpan.FromSeconds(5);
        public static int CFG_HELLO_TASK_RETRIES_MAX = 2;
        public static int JITTEST_RESPONSE_WINDOW_SIZE = 256;

        public static int MaxInteractionTimeGapSeconds = 10;
    }
}
