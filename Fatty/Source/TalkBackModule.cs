using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        public TalkBackModule()
        {
            IsDefaultModule = true;
        }

        public override void RegisterEvents(IRCConnection connection)
        {
            connection.ChannelMessageEvent += OnChannelMessage;
        }

        void OnChannelMessage(IRCConnection connection, string ircUser, string ircChannel, string message)
        {
            if(message.Contains(connection.Context.Nick))
            {
                connection.SendMessage(ircChannel, "fuq u");
            }
        }
    }
}
