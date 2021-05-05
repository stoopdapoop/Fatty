using System;
using System.Collections.Generic;
using System.Text;

namespace Fatty
{
    class EmailModule : FattyModule
    {
        public EmailModule()
        {

        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
        }

        public override void RegisterEvents()
        {
            base.RegisterEvents();

            OwningChannel.ChannelMessageEvent += OnChannelMessage;
        }

        void OnChannelMessage(string ircUser, string message)
        {
            if(message.StartsWith(OwningChannel.CommandPrefix))
            {
                var chunks = message.Split(" ");
                string CommandName = chunks[0].Remove(0, OwningChannel.CommandPrefix.Length).ToLower();

                switch(CommandName)
                {
                    case "email":
                        if(chunks.Length > 2)
                            SendEmail(chunks[1], ircUser, String.Join(" ", chunks, 2, chunks.Length - 2));
                        break;
                }
            }
        }

        void SendEmail(string to, string from, string message)
        {
            bool success = Fatty.SendEmail(to, String.Format("A message from {0} in {1}", from, OwningChannel.ChannelName), message);
            if(success)
            {
                OwningChannel.SendMessage(String.Format("sent \"{0}\" to {1}", message, to), from);
            }
            else
            {
                OwningChannel.SendMessage(String.Format("failed to send \"{0}\" to {1}", message, to), from);
            }
        }
    }
}
