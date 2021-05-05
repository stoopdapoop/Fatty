using System;

namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        private static string[] Greetings = { "heddo", "hi", "herro", "hi der", "ayyo", "hey" };
        private static string[] PetNames = { "bb", "cutie", "babbycakes", "qt", "str8boi" };

        public TalkBackModule()
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
            if (message.StartsWith(OwningChannel.CommandPrefix))
            {
                int spacePos = message.IndexOf(" ");
                int commandPrefixLength = OwningChannel.CommandPrefix.Length;
                string CommandName;
                if(spacePos == -1)
                    CommandName = message.Substring(commandPrefixLength).ToLower();
                else
                    CommandName = message.Substring(commandPrefixLength, spacePos - commandPrefixLength).ToLower();

                switch (CommandName)
                {
                    case "noel":
                        OwningChannel.SendMessage("NO!", ircUser);
                        break;
                }
            }
            else
            {
                if (message.Contains(OwningChannel.GetFattyNick()))
                {
                    RandomGreeting(ircUser, message);
                }
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
    }
}
