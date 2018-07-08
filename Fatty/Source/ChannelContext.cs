using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Linq;

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

        [OnDeserialized]
        private void DeserializationInitializer(StreamingContext ctx)
        {
            // get rid of any nulls that might have cropped up due to deserialization
            if (FeatureBlacklist == null)
                FeatureBlacklist = new List<string>();

            if (CommandBlacklist == null)
                CommandBlacklist = new List<string>();

            if (FeatureWhitelist == null)
                FeatureWhitelist = new List<string>();

            if (CommandWhitelist == null)
                CommandWhitelist = new List<string>();
        }

        public void Initialize(ServerContext server)
        {
            Server = server;
            Server.ChannelMessageEvent += HandleChannelMessage;

            foreach (Type moduleType in Fatty.GetModuleTypes)
            {
                bool shouldInstantiate = false;
                string moduleName = moduleType.Name;
                if (Fatty.GetDefaultModuleTypes.Any(x => x.Name == moduleName))
                {
                    if (!FeatureBlacklist.Contains(moduleName))
                    {
                        shouldInstantiate = true;
                    }     
                }
                else if(FeatureWhitelist.Contains(moduleName))
                {
                    shouldInstantiate = true;
                }

                if(shouldInstantiate)
                {
                    FattyModule module = (FattyModule)Activator.CreateInstance(moduleType);
                    module.Init(this);
                }
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
