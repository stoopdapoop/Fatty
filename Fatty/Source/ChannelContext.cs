using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class ChannelContext
    {
        public event ChannelMessageDelegate ChannelMessageEvent;

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

        private ServerContext Server;

        public void Initialize(ServerContext server)
        {
            Server = server;
            Server.ChannelMessageEvent += HandleChannelMessage;
            // init plugins
        }

        private void HandleChannelMessage(string ircUser, string ircChannel, string message)
        {
            Console.WriteLine(message);
        }

        // does filtering by blacklist and whitelist for modules
        public void AddChannelMessageCallback(ChannelMessageDelegate callback)
        {
        }
        
    }
}
