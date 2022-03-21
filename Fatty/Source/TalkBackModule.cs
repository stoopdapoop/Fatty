using System;
using System.Collections.Generic;

namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        private static string[] Greetings = { "hi", "hi der", "ayyo", "hey", "ayy", "yo", "whattup", "hi hi", "ey", "hello" };
        private static string[] PetNames = { "bb", "cutie", "babbycakes", "qt", "my wide friend", "bruh" };

        public TalkBackModule()
        {

        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
        }


        public override void GetAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("Noel", NoelCommand, "returns what noel would say in this situation"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("Noel");
        }

        public override void RegisterEvents()
        {
            base.RegisterEvents();

            OwningChannel.ChannelMessageEvent += OnChannelMessage;
        }

        void OnChannelMessage(string ircUser, string message)
        {
            if (message.Contains(OwningChannel.GetFattyNick(),StringComparison.CurrentCultureIgnoreCase))
            {
                RandomGreeting(ircUser, message);
            }
        }

        private void RandomGreeting(string instigator, string message)
        {
            Random Rand = new Random();

            int coinFlip = Rand.Next(0, 2);
            if (coinFlip == 0)
            {
                OwningChannel.SendChannelMessage(Greetings[Rand.Next(Greetings.Length)]);
            }
            else
            {
                string greeting = Greetings[Rand.Next(Greetings.Length)];
                string petName = PetNames[Rand.Next(PetNames.Length)];
                OwningChannel.SendChannelMessage(greeting + " " + petName);
            }
        }

        private void NoelCommand(string ircUser, string ircChannel, string message)
        {
            OwningChannel.SendMessage("NO!", ircUser);
        }
    }
}
