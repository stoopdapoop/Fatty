﻿using System;

namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        public TalkBackModule()
        {
            IsDefaultModule = true;
        }

        public override void RegisterEvents()
        {
            //connection.AddChannelMessageCallback(OnChannelMessage);
        }

        void OnChannelMessage(string ircUser, string ircChannel, string message)
        {

        }
    }
}
