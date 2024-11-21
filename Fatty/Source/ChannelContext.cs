using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace Fatty
{
    [DataContract]
    public class ChannelContext
    {
        public event PluginChannelMessageDelegate ChannelMessageEvent;
        public event PluginChannelJoinedDelegate ChannelJoinedEvent;
        public event UserJoinPartDelegate UserJoinedEvent;
        public event UserstateDelegate UserstateEvent;
        public event UserNoticeDelegate UserNoticeEvent;

        [DataMember(IsRequired = true)]
        public string ChannelName { get; set; }

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

        [DataMember]
        public bool LoggingDisabled { get; set; }

        public string ServerName => Server.ServerName;

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
            Server.UserJoinedEvent += HandleUserJoined;
            Server.UserstateEvent += HandleUserstate;
            Server.UserNoticeEvent += HandleUserNotice;

            RegisterGlobalCommands();

            foreach (Type moduleType in Fatty.GetModuleTypes)
            {
                bool shouldInstantiate = false;
                string moduleName = moduleType.Name;
                if(FeatureWhitelist.Contains(moduleName))
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
                    module.RegisterAvailableCommands(ref ModuleCommands);

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

        public void SendCapMessage(string cap)
        {
            Server.SendCapMessage(cap);
        }

#nullable enable
        public DateTime? GetUserLastSeenTime(string user, string ircChannel, out bool everSeen)
        {
            everSeen = false;
            Debug.Assert(ircChannel == this.ChannelName, "Inconsistent ChannelName");


            //string[] connectedUsers = Server.GetConnectedChannelUsers(this);

            //foreach(string connectedUser in connectedUsers)
            //{
            //    if (connectedUser == user)
            //    {
            //        return DateTime.Now;
            //    }
            //}

            IrcLogUser? foundUser = Server.GetLoggedUserInfo(user);
            if (foundUser != null) 
            {
                everSeen = true;
                // this value was added after table creation and I didn't fix up old values
                if(foundUser.LastSeen != null)
                {
                    return foundUser.LastSeen;
                }
            }

            return null;
        }
#nullable disable

        private void HandleChannelMessage(Dictionary<string, string>? tags, string ircUser, string ircChannel, string message)
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
                    chanDel(tags, ircUser, message);
                }
            }
        }

        private void HandleUserJoined(string ircUser, string ircChannel, JoinType type)
        {
            if (UserJoinedEvent != null)
            {
                foreach (UserJoinPartDelegate chanDel in UserJoinedEvent.GetInvocationList())
                {
                    chanDel(ircUser, ircChannel, type);
                }
            }
        }

        private void HandleChannelJoined(string ircChannel)
        {
            if (ChannelJoinedEvent != null)
            {
                foreach (PluginChannelJoinedDelegate chanDel in ChannelJoinedEvent.GetInvocationList())
                {
                    chanDel(ircChannel);
                }
            }
        }

        private void HandleUserstate(UserStateType type, Dictionary<string, string>? tags, string channel, string username)
        {
            if(UserstateEvent != null)
            {
                foreach(UserstateDelegate usDel  in UserstateEvent.GetInvocationList())
                {
                    usDel(type, tags, channel, username);
                }
            }
        }

        private void HandleUserNotice(Dictionary<string, string>? tags, string channel, string username)
        {
            if(UserNoticeEvent != null)
            {
                foreach(UserNoticeDelegate unDel in UserNoticeEvent.GetInvocationList())
                {
                    unDel(tags, channel, username);
                }
            }
        }

        private void RegisterGlobalCommands()
        {
            AvailableCommands.Add("help", new UserCommand("Help", HelpCommand, "Provides help for given command, returns all commands if called with no argument"));
            AvailableCommands.Add("commands", new UserCommand("Commands", CommandsCommand, "Returns a list of all available commands on this channel."));
            AvailableCommands.Add("modules", new UserCommand("Modules", ModulesCommand, "Returns a list of all active modules on this channel."));
        }

        private void HelpCommand(string ircUser, string ircChannel, string message)
        {
            message = message.TrimEnd();
            string[] segments = message.Split(" ");
            if(segments.Length > 1)
            {
                UserCommand FoundCommand;
                AvailableCommands.TryGetValue(segments[1], out FoundCommand);
                if( FoundCommand != null)
                {
                    Server.SendMessage(ircChannel, FoundCommand.CommandHelp);
                }
                else
                {
                    Server.SendMessage(ircChannel, $"Don't know of a command named {segments[1]}");
                }
            }
            else
            {
                CommandsCommand(ircUser, ircChannel, message);
            }
        }

        private void CommandsCommand(string ircUser, string ircChannel, string message)
        {
            StringBuilder sb = new StringBuilder();

            foreach(var command in AvailableCommands)
            {
                sb.Append($"{command.Value.CommandName} ");
            }

            Server.SendMessage(ircChannel, sb.ToString());
        }

        private void ModulesCommand(string ircUser, string ircChannel, string message)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var mod in ActiveModules)
            {
                sb.Append($"{mod.ToString()} ");
            }

            Server.SendMessage(ircChannel, sb.ToString());
        }

        public void PostConnectionInitModules()
        {
            foreach(FattyModule module in ActiveModules)
            {
                module.PostConnectionModuleInit();
            }
        }

    }
}

