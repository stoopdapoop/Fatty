﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Fatty
{
    public class TwitchModule : FattyModule
    {
        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            
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
            // todo: implement system for handling per-server, per-channel, and per api endpoint data.
            OwningChannel.SendCapMessage(@"twitch.tv/membership");
            OwningChannel.SendCapMessage(@"twitch.tv/commands");
            //OwningChannel.SendCapMessage(@"twitch.tv/tags");
        }

        private void OnUserJoined(string ircUser, string ircChannel, JoinType type)
        {

        }
    }
}
