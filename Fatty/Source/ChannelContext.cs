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
        public event PluginChannelJoinedDelegate ChannelJoinedEvent;

        [DataMember(IsRequired = true)]
        public string ChannelName { get; set; }

        [DataMember(IsRequired = true)]
        public bool UseDefaultFeatures { get; set; }

        [DataMember]
        public bool SilentMode = false;

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

        private List<FattyModule> ActiveModules;

        private Dictionary<string, UserCommand> AvailableCommands;

        [OnDeserialized]
        private void DeserializationInitializer(StreamingContext ctx)
        {
            DefaultInitializePermissionLists();
        }

        private void DefaultInitializePermissionLists()
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

            CommandBlacklist.ForEach(c => c = c.ToLower());
        }

        public void Initialize(ServerContext server)
        {
            if(CommandPrefix == null)
                CommandPrefix = server.CommandPrefix;

            DefaultInitializePermissionLists();

            ActiveModules = new List<FattyModule>();
            AvailableCommands = new Dictionary<string, UserCommand>();

            Server = server;
            Server.ChannelMessageEvent += HandleChannelMessage;
            Server.ChannelJoinedEvent += HandleChannelJoined;

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
                    Fatty.PrintToScreen("Initializing {0} in {1}", module.ToString(), ChannelName);
                    module.ChannelInit(this);
                    module.RegisterEvents();
                    ActiveModules.Add(module);

                    List<UserCommand> ModuleCommands = new List<UserCommand>();
                    module.GetAvailableCommands(ref ModuleCommands);

                    foreach(UserCommand command in ModuleCommands)
                    {
                        string commandName = command.CommandName.ToLower();
                        if(!CommandBlacklist.Contains(commandName))
                        {
                            AvailableCommands.Add(commandName, command);
                        }
                    }
                }
            }
        }

        public string GetFattyNick()
        {
            return Server.Nick;
        }

        public bool SendMessage(string message, string instigator)
        {
            if (SilentMode)
            {
                Server.SendMessage(instigator, message);
                return false;
            }
            else
            {
                Server.SendMessage(ChannelName, message);
                return true;
            }
        }

        public void SendChannelMessage(string message)
        {
            if (!SilentMode)
                Server.SendMessage(ChannelName, message);
        }

        private void HandleChannelMessage(string ircUser, string ircChannel, string message)
        {
            if (message.StartsWith(CommandPrefix))
            {
                int spacePos = message.IndexOf(" ");
                int commandPrefixLength = CommandPrefix.Length;
                string CommandName;
                if (spacePos == -1)
                    CommandName = message.Substring(commandPrefixLength).ToLower();
                else
                    CommandName = message.Substring(commandPrefixLength, spacePos - commandPrefixLength).ToLower();

                UserCommand FoundCommand;
                AvailableCommands.TryGetValue(CommandName, out FoundCommand);
                if(FoundCommand != null)
                {
                    FoundCommand.CommandCallback(ircUser, ircChannel, message);
                }
            }

            if (ChannelMessageEvent != null)
            {
                foreach (PluginChannelMessageDelegate chanDel in ChannelMessageEvent.GetInvocationList())
                {
                    FattyModule DelegateModule = (FattyModule)chanDel.Target;
                    chanDel(ircUser, message);
                }
            }
        }

        private void HandleChannelJoined(string ircChannel)
        {
            if (ChannelJoinedEvent != null)
            {
                foreach (PluginChannelJoinedDelegate chanDel in ChannelJoinedEvent.GetInvocationList())
                {
                    FattyModule DelegateModule = (FattyModule)chanDel.Target;
                    chanDel(ircChannel);
                }
            }
        }
    }
}
