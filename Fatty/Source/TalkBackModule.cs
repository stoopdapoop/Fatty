using System;

namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        private static string[] Greetings = {"heddo", "hi", "herro", "hi der", "ayyo"};
        static Random Rand = new Random();

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
            if(message.Contains(OwningChannel.GetFattyNick()))
            {
                OwningChannel.SendChannelMessage(Greetings[Rand.Next(Greetings.Length)]);
            }
        }
    }
}
