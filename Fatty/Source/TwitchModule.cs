using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Fatty
{
    public class TwitchModule : FattyModule
    {
        [DataContract]
        public class TwitchContextListing
        {
            [DataMember(IsRequired = true)]
            public string ClientID;

            [DataMember(IsRequired = true)]
            public string RedirectURI;

            [DataMember(IsRequired = true)]
            public string ClientSecret;

            [DataMember(IsRequired = true)]
            public string AccessToken;

            [DataMember(IsRequired = true)]
            public List<string> BotScopes;

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
            public string AccessToken { get; set; }

            [DataMember(Name = "refresh_token")]
            public string RefreshToken { get; set; }

            [DataMember(Name = "expires_in")]
            public int AccessExpiresInSeconds { get; set; }
        }

        private class TwitchEndpoints
        {
            public const string BaseOAuth = "https://id.twitch.tv/oauth2";
        }

        private static TwitchTokenResponse TokenResponse;

        private bool IsTwitchChannel;

        private static TwitchContextListing GlobalData;
        TwitchContext ActiveContext;

        const string TokenPath = "TwitchTokens.pls";


        static Dictionary<string, PluginChannelMessageDelegate> TwitchMessageEvents;
        static Dictionary<string, PluginChannelMessageDelegate> IRCMirrorMessageEvents;

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("TwitchAuthURL", GenTwitchAuthTokenURL, "Generates a twitch Oath url for fatty"));
            Commands.Add(new UserCommand("TwitchRefreshToken", RefreshOAuthTokenCommand, "Refreshes OAuth Tokens. Pass in an access token as argument to validate, otherwise last refresh token is used."));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("TwitchAuthURL");
        }

        public static string GetAuthScopesString()
        {
            return string.Join(" " ,GlobalData.BotScopes);
        }

        private string GetMirrorKey(string server, string channel)
        {
            return $"{server}/{channel}";
        }

        private void RefreshOAuthToken(TwitchContextListing globals, string? newAccessToken  = null)
        {

            TwitchTokenResponse oldValues = FattyHelpers.DeserializeFromPath<TwitchTokenResponse>(TokenPath);

            string refreshToken = oldValues.RefreshToken;

            var formData = new Dictionary<string, string>();

            if (newAccessToken == null)
            {
                formData.Add("client_id", globals.ClientID);
                formData.Add("client_secret", globals.ClientSecret);
                formData.Add("grant_type", "refresh_token");
                formData.Add("refresh_token", refreshToken);
            }
            else
            {
                formData.Add("client_id", globals.ClientID);
                formData.Add("client_secret", globals.ClientSecret);
                formData.Add("code", newAccessToken);
                formData.Add("grant_type", "authorization_code");
                formData.Add("redirect_uri", globals.RedirectURI);
            }

            try
            {
                HttpResponseMessage result = FattyHelpers.HttpRequest("https://id.twitch.tv", "oauth2/token", HttpMethod.Post, null, null, formData).Result;

                if (result.IsSuccessStatusCode)
                {
                    string returnResult = result.Content.ReadAsStringAsync().Result;
                    oldValues = FattyHelpers.DeserializeFromJsonString<TwitchTokenResponse>(returnResult);
                    FattyHelpers.JsonSerializeToPath(oldValues, TokenPath);
                    GlobalData.AccessToken = oldValues.AccessToken;
                    DateTime Expiration = DateTime.Now + TimeSpan.FromSeconds(oldValues.AccessExpiresInSeconds);
                    OwningChannel.SendChannelMessage($"Sucessfully refreshed auth token. It will expire at {Expiration.ToString()}. {(newAccessToken != null ? "Don't forget to update twitch config." : "")}");
                }
                else
                {
                    Fatty.PrintWarningToScreen($"Failed to refresh twitch auth token: {result.ReasonPhrase}");
                    OwningChannel.SendChannelMessage($"Failed to refresh twitch auth token: {result.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Fatty.PrintWarningToScreen(ex);
            }
        }

        private async void ValidateOAuthToken()
        {
            while (true)
            {
                try
                {
                    var headers = FattyHelpers.CreateHTTPRequestHeaders();
                    headers.Add("Authorization", $"OAuth {GlobalData.AccessToken}");

                    HttpResponseMessage result = await FattyHelpers.HttpRequest("https://id.twitch.tv", "oauth2/validate", HttpMethod.Get, null, headers);

                    if (!result.IsSuccessStatusCode)
                    {
                        Fatty.PrintWarningToScreen(result.Content.ReadAsStringAsync().Result);
                    }
                }
                catch (Exception ex)
                {
                    Fatty.PrintWarningToScreen(ex);
                }

                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private void RefreshOAuthTokenCommand(string ircUser, string ircChannel, string message)
        {
            string[] segments = message.Split(" ");
            string newAccessToken = null;
            if (segments.Length == 2)
            {
                newAccessToken = segments[1];
            }

            RefreshOAuthToken(GlobalData, newAccessToken);
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

                ValidateOAuthToken();
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

        private void GenTwitchAuthTokenURL(string ircUser, string ircChannel, string message)
        {
            UriBuilder uriBuilder = new UriBuilder("https://id.twitch.tv")
            {
                Path = "oauth2/authorize"
            };
            NameValueCollection queryData = HttpUtility.ParseQueryString(uriBuilder.Query);
            queryData["client_id"] = GlobalData.ClientID;
            queryData["redirect_uri"] = GlobalData.RedirectURI;
            queryData["response_type"] = "code";
            queryData["scope"] = GetAuthScopesString();
            queryData["state"] = Guid.NewGuid().ToString("N");
            uriBuilder.Query = queryData.ToString();

            OwningChannel.SendChannelMessage(uriBuilder.Uri.ToString());
        }
    }
}
