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

        public override void Init(ChannelContext channel)
        {
            base.Init(channel);

            OwningChannel.ChannelMessageEvent += OnChannelMessage;
        }

        void OnChannelMessage(string ircUser, string message)
        {
            Random Rand = new Random();
            if (message.Contains(OwningChannel.GetFattyNick()))
            {
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
}
