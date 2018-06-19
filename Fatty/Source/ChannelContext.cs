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

        // todo: replace with servercontext
        private IRCConnection OwningConnection;

        public void Initialize(IRCConnection owner)
        {
            OwningConnection = owner;
            OwningConnection.ChannelMessageEvent += HandleChannelMessage;
        }

        private void HandleChannelMessage(string ircUser, string ircChannel, string message)
        {
            // check against plugins
            Console.WriteLine("Ayyyy");
        }

        // does filtering by blacklist and whitelist
        public void AddChannelMessageCallback(ChannelMessageDelegate callback)
        {
            OwningConnection.ChannelMessageEvent += callback;
        }
        
    }
}
