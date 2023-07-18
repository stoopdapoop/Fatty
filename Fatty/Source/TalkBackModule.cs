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


        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("Noel", NoelCommand, "returns what noel would say in this situation"));
            Commands.Add(new UserCommand("dpaste", DpasteCommand, "Gets a link to dpaste."));
            Commands.Add(new UserCommand("seen", SeenCommand, "Reports the last time the user spoke, joined, or parted"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("Noel");
            CommandNames.Add("dpaste");
            CommandNames.Add("seen");
        }

        public override void RegisterEvents()
        {
            base.RegisterEvents();

            OwningChannel.ChannelMessageEvent += OnChannelMessage;
        }

        void OnChannelMessage(string ircUser, string message)
        {
            if (message.Contains(OwningChannel.GetFattyNick()))
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

        private void DpasteCommand(string ircUser, string ircChannel, string message)
        {
            OwningChannel.SendMessage("https://dpaste.org/", ircUser);
        }


        private void SeenCommand(string ircUser, string ircChannel, string message)
        {
            bool everSeen = false;

            string[] chunks = message.Split(" ");
            if(chunks.Length < 2) 
            {
                OwningChannel.SendMessage("I need a name", ircUser);
                return;
            }
            string findName = chunks[1];
#nullable enable
            var lastSeen = OwningChannel.GetUserLastSeenTime(findName, ircChannel, out everSeen);
            if (lastSeen == null)
            {
                if (!everSeen)
                {
                    OwningChannel.SendMessage($"Never seen {findName} before", ircUser);
                }
                else
                {
                    OwningChannel.SendMessage($"I've seen {findName} before, but not since I started tracking seen time. Sorry :[", ircUser);
                }
            }
#nullable disable
            else
            {
                OwningChannel.SendMessage($"Last seen {findName} on {lastSeen}, though they might be around now...", ircUser);
            }
            
        }
        
    }
}
