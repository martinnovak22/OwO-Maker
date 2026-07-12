using System.Threading;
using OwO_Maker.Core;

namespace OwO_Maker
{
    class BotEntry
    {
        public int BotId;
        public nint ClientHwnd;
        public Thread Thread;
        public BotControl Control = new();
        public BotStats Stats = new();
        public bool ThreadStarted;
    }
}
