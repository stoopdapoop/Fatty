using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    // Class that exposes events to modules
    class ConnectionInterface
    {
        public event ChannelMessage ChannelMessageEvent;
        public event PrivateMessage PrivateMessageEvent;
        public event TopicSet TopicSetEvent;
        public event TopicOwner TopicOwnerEvent;
        public event NamesList NamesListEvent;
        public event ServerMessage ServerMessageEvent;
        public event Join JoinEvent;
        public event Part PartEvent;
        public event Mode ModeEvent;
        public event NickChange NickChangeEvent;
        public event Kick KickEvent;
        public event Quit QuitEvent;
        public event Notice NoticeEvent;
        public event AnyMessage AnyMessageEvent;

        private IRCConnection OwningConnection;

        public ConnectionInterface(IRCConnection owner)
        {
            OwningConnection = owner;
        }
    }
}
