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

        public override void Init(ChannelContext channel)
        {
            base.Init(channel);

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
                            SendEmail(chunks[1], String.Join(" ", chunks, 2, chunks.Length - 3));
                        break;
                }
            }
        }

        void SendEmail(string to, string message)
        {
            bool success = Fatty.SendEmail(to, "test", message);
            if(success)
            {
                OwningChannel.SendChannelMessage(String.Format("sent \"{0}\" to {1}", message, to));
            }
            else
            {
                OwningChannel.SendChannelMessage(String.Format("failed to send \"{0}\" to {1}", message, to));
            }
        }
    }
}
