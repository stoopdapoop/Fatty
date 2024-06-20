using System;
using System.Collections.Generic;
using System.Web;

namespace Fatty
{
    public class TwitchModule : FattyModule
    {
        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("GenTwitchAuthURL", GenTwitchAuthURL, "Generates a twitch Oath url for fatty, Params are {RedirectURI}"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("GenTwitchAuthURL");
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
        }

        public override void RegisterEvents()
        {
            base.RegisterEvents();


            OwningChannel.UserJoinedEvent += OnUserJoined;
            OwningChannel.ChannelJoinedEvent += OnChannelJoined;
        }

        private void OnChannelJoined(string ircChannel)
        {
        }

        private void OnUserJoined(string ircUser, string ircChannel, JoinType type)
        {

        }

        private void GenTwitchAuthURL(string ircUser, string ircChannel, string message)
        {
            string encoded = HttpUtility.UrlEncode("scope=chat:read+chat:edit+whispers:read+whispers:edit+channel:moderate");
            Fatty.PrintToScreen(encoded, ConsoleColor.Magenta);
        
        }

    }
}
