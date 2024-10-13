using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Web;

namespace Fatty
{
    public class TwitchModule : FattyModule
    {
        [DataContract]
        public class TwitchContextListing
        {
            [DataMember(IsRequired = true, EmitDefaultValue = true)]
            public string ClientID;

            [DataMember(IsRequired = true, EmitDefaultValue = true)]
            public string RedirectURI;

            [DataMember(IsRequired = true)]
            public string ClientSecret;

            [DataMember(IsRequired = true)]
            public string BotScope;

            [DataMember(IsRequired = true, Name = "TwitchContexts")]
            public List<TwitchContext> AllContexts;
            
            [DataMember]
            public List<string> BannedPhrases;
        }

        [DataContract]
        public class TwitchContext
        {
            [DataMember(IsRequired = true)]
            public string TwitchChatChannel;

            [DataMember(IsRequired = false)]
            public string IRCMirrorServerName;

            [DataMember(IsRequired = false)]
            public string IRCMirrorChannelName;
        }

        [DataContract]
        public class TwitchTokenResponse
        {
            [DataMember(Name = "access_token")]
            public string AccessToken { get; private set; }

            [DataMember(Name = "refresh_token")]
            public string RefreshToken { get; private set; }

            [DataMember(Name = "expires_in")]
            public int AccessExpiresInSeconds { get; private set; }
        }

        private static TwitchTokenResponse TokenResponse;

        private bool IsTwitchChannel;

        private static TwitchContextListing GlobalData;
        TwitchContext ActiveContext;


        static Dictionary<string, PluginChannelMessageDelegate> TwitchMessageEvents;
        static Dictionary<string, PluginChannelMessageDelegate> IRCMirrorMessageEvents;

        public static TwitchTokenResponse ValidateAuthTokens()
        {
            return null;
        }

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("GenTwitchAuthURL", GenTwitchAuthURL, "Generates a twitch Oath url for fatty, Params are {RedirectURI}"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("GenTwitchAuthURL");
        }

        private string GetMirrorKey(string server, string channel)
        {
            return $"{server}/{channel}";
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
            IsTwitchChannel = OwningChannel.ServerName == "twitch";

            if (GlobalData == null)
            {
                TwitchMessageEvents = new Dictionary<string, PluginChannelMessageDelegate>();
                IRCMirrorMessageEvents = new Dictionary<string, PluginChannelMessageDelegate>();

                GlobalData = FattyHelpers.DeserializeFromPath<TwitchContextListing>("Twitch.cfg");
            }

            foreach (var context in GlobalData.AllContexts)
            {
                if (IsTwitchChannel && context.TwitchChatChannel == channel.ChannelName)
                {
                    ActiveContext = context;
                }
                else
                {
                    if (context.IRCMirrorChannelName == channel.ChannelName && context.IRCMirrorServerName == channel.ServerName)
                    {
                        ActiveContext = context;
                    }
                }
            }

            if (ActiveContext == null)
            {
                Fatty.PrintWarningToScreen($" No active context set in {channel.ServerName}/{channel.ChannelName}");
            }
            else
            {
                if(ActiveContext.IRCMirrorChannelName == channel.ChannelName && ActiveContext.IRCMirrorServerName == OwningChannel.ServerName)
                {
                    if(!OwningChannel.LoggingDisabled)
                    {
                        Fatty.PrintWarningToScreen($"!!!!!!!!!!!Logging is enabled in twitch mirror channel : {channel.ServerName}/{channel.ChannelName}");
                    }
                }
            }
        }


        public override void RegisterEvents()
        {
            base.RegisterEvents();

            OwningChannel.UserJoinedEvent += OnUserJoined;
            OwningChannel.ChannelJoinedEvent += OnChannelJoined;
            OwningChannel.ChannelMessageEvent += OnChannelMessage;

            if(IsTwitchChannel)
            {
                IRCMirrorMessageEvents[GetMirrorKey("twitch", ActiveContext.TwitchChatChannel)] = OnMirrorChannelIRCMessage;
            }
            else
            {
                TwitchMessageEvents[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)] = OnMirrorTwitchMessage;
            }
        }

        

        public override void PostConnectionModuleInit()
        {
            base.PostConnectionModuleInit();
        }

        private void OnChannelJoined(string ircChannel)
        {

        }

        private void OnUserJoined(string ircUser, string ircChannel, JoinType type)
        {

        }

        private void OnChannelMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {

            if (IsTwitchChannel)
            {
                TwitchMessageEvents[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)].Invoke(tags, ircUser, message);
                ProcessUserMessage(tags, ircUser, message);
            }
            else
            {
                IRCMirrorMessageEvents[GetMirrorKey("twitch", ActiveContext.TwitchChatChannel)].Invoke(tags, ircUser, message);
            }
        }

        private void ProcessUserMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {
            if(tags != null)
            {
                string firstMessage;
                if(tags.TryGetValue("first-msg", out firstMessage))
                {
                    if(firstMessage == "1")
                    {
                        FilterNewChatterMessages(tags, ircUser, message);
                    }
                }
            }
        }

        private void FilterNewChatterMessages(Dictionary<string, string>? tags, string ircUser, string message)
        {
            string strippedMessage = FattyHelpers.RemoveDiacritics(message);
            
            string matchedString = GlobalData.BannedPhrases.FirstOrDefault(s => strippedMessage.Contains(s, StringComparison.OrdinalIgnoreCase));
            if (matchedString != null)
            {
                OwningChannel.SendChannelMessage($"STFU, {matchedString} ass bot");
            }
        }

        // used to mirror irc messages on twitch
        private void OnMirrorChannelIRCMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {
            // too spicy
            //OwningChannel.SendChannelMessage(message);
        }

        // used to mirror twitch messages onto irc
        private void OnMirrorTwitchMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {

            string colorString = string.Empty;
            int colorIndex = 99; 
            if (tags != null)
            {
                tags.TryGetValue("color", out colorString);
                colorIndex = Colors.GetNearestIrcColor(colorString);
                
                // assign random color based on name if none provided
                if(colorIndex == -1)
                {
                    colorIndex = ircUser.GetHashCode() % 99;
                }
            }
            

            OwningChannel.SendChannelMessage($"{(colorIndex != -1 ? $"\x3{colorIndex}" : "")}<{ircUser}>\x0F{message}");
        }

        private void GenTwitchAuthURL(string ircUser, string ircChannel, string message)
        {
            //string endpoint = "https://id.twitch.tv/oauth2/token";
            //string encoded = HttpUtility.UrlEncode("scope=chat:read+chat:edit+whispers:read+whispers:edit+channel:moderate");
            //Fatty.PrintToScreen(encoded, ConsoleColor.Magenta);
        }
    }
}
