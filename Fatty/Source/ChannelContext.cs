using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class ChannelContext
    {
        public event PluginChannelMessageDelegate ChannelMessageEvent;

        [DataMember(IsRequired = true)]
        public string ChannelName { get; set; }

        [DataMember(IsRequired = true)]
        public bool UseDefaultFeatures { get; set; }

        [DataMember]
        public List<string> FeatureBlacklist;

        [DataMember]
        public List<string> CommandBlacklist;

        [DataMember]
        public List<string> FeatureWhitelist;

        [DataMember]
        public List<string> CommandWhitelist;

        [DataMember]
        public string CommandPrefix { get; private set; }

        private ServerContext Server;

        public void Initialize(ServerContext server)
        {
            Server = server;
            Server.ChannelMessageEvent += HandleChannelMessage;

            foreach (Type moduleType in Fatty.GetDefaultModuleTypes)
            {
                FattyModule module = (FattyModule)Activator.CreateInstance(moduleType);
                module.Init(this);
            }
        }

        public string GetFattyNick()
        {
            return Server.Nick;
        }

        public bool SendChannelMessage(string message)
        {
            //todo: filter based on permissions
            Server.SendMessage(ChannelName, message);

            return true;
        }

        private void HandleChannelMessage(string ircUser, string ircChannel, string message)
        {
            if (ChannelMessageEvent != null)
            {
                foreach (PluginChannelMessageDelegate chanDel in ChannelMessageEvent.GetInvocationList())
                {
                    FattyModule DelegateModule = (FattyModule)chanDel.Target;
                    // todo: based on permissions
                    //if (DelegateModule.ModuleName == ircChannel)
                    {
                        chanDel.BeginInvoke(ircUser, message, null, null);
                    }
                }
            }
        }
    }
}
