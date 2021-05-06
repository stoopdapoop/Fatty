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

        public override void GetAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("Email", EmailCommand, @"params are : [EmailAddress] [Message]"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("Email");
        }

        public override void RegisterEvents()
        {
            base.RegisterEvents();
        }

        void EmailCommand(string ircUser, string ircChannel, string message)
        {
            string[] chunks = message.Split(" ");

            if (chunks.Length > 2)
                SendEmail(chunks[1], ircUser, String.Join(" ", chunks, 2, chunks.Length - 2));
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
