using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace Fatty
{


    public class TwitchModule : FattyModule
    {
        [DataContract]
        public class TwitchContextListing
        {
            [DataMember(IsRequired = true)]
            public string BotAccountName;

            [DataMember(IsRequired = true)]
            public string ClientID;

            [DataMember(IsRequired = true)]
            public string RedirectURI;

            [DataMember(IsRequired = true)]
            public string ClientSecret;

            [DataMember(IsRequired = true)]
            public List<string> BotScopes;

            [DataMember(IsRequired = true)]
            public List<string> UserScopes;

            [DataMember(IsRequired = true, Name = "TwitchContexts")]
            public List<TwitchContext> AllContexts;

            [DataMember]
            public List<string> BannedPhrases;

            [OnDeserialized]
            private void DeserializationInitializer(StreamingContext ctx)
            {
                for (int i = 0; i < BannedPhrases.Count; ++i)
                {
                    BannedPhrases[i] = BannedPhrases[i].RemoveWhitespace();
                }
            }
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

            [DataMember(IsRequired = false)]
            public bool ShouldWelcomeRaiders;
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

            [DataMember(Name = "scope")]
            public string[] TokenScopes { get; set; }

            [DataMember(IsRequired = false)]
            public string TwitchUsername;

            [DataMember(IsRequired = false)]
            public string IRCUsername;
        }

        [DataContract]
        public class TwitchTokenResponseStore
        {
            [DataMember(IsRequired = false, Name = "Tokens")]
            public TwitchTokenResponse[] TokenResponses;
            
            public int GetIndexByTwitchUser(string username)
            {
                for(int i = 0; i < TokenResponses.Length; ++i)
                {
                    if (TokenResponses[i].TwitchUsername != null && TokenResponses[i].TwitchUsername.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
                return -1;
            }

            public int GetIndexByIRCUser(string username)
            {
                for (int i = 0; i < TokenResponses.Length; ++i)
                {
                    if (TokenResponses[i].IRCUsername != null && TokenResponses[i].IRCUsername.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
                return -1;
            }

            public int GetFattyTokenIndex()
            {
                int emptyNameCount = 0;
                int returnVal = -1;
                for (int i = 0; i < TokenResponses.Length; ++i)
                {
                    if (TokenResponses[i].IRCUsername == null)
                    {
                        ++emptyNameCount;
                        returnVal = i;
                    }
                }

                if (emptyNameCount > 1)
                {
                    throw new Exception("Too many candidate fatty tokens");
                }

                return returnVal;
            }

        }

        [DataContract]
        public class TwitchUserResponse
        {
            [DataMember(Name = "data")]
            public TwitchUser[] Data { get; set; }
        }

        [DataContract]
        public class TwitchUser
        {
            [DataMember(Name = "id")]
            public string Id { get; set; }

            [DataMember(Name = "login")]
            public string Login { get; set; }

            [DataMember(Name = "display_name")]
            public string DisplayName { get; set; }

            [DataMember(Name = "type")]
            public string Type { get; set; }  // User, Admin, etc.

            [DataMember(Name = "broadcaster_type")]
            public string BroadcasterType { get; set; }  // Affiliate, Partner, etc.

            [DataMember(Name = "description")]
            public string Description { get; set; }

            [DataMember(Name = "profile_image_url")]
            public string ProfileImageUrl { get; set; }

            [DataMember(Name = "offline_image_url")]
            public string OfflineImageUrl { get; set; }
        }

        private class BanRequest
        {
            [JsonPropertyName("user_id")]
            public string UserId { get; set; }

            [JsonPropertyName("duration")]
            public int? Duration { get; set; }  

            [JsonPropertyName("reason")]
            public string Reason { get; set; }
        }

        private class BanRequestWrapper
        {
            [JsonPropertyName("data")]
            public BanRequest Data { get; set; }
        }

        [DataContract]
        private class SendMessageRequest
        {
            [DataMember(Name = "broadcaster_id")]
            //[JsonPropertyName("broadcaster_id")]
            public string BroadcasterID;

            [DataMember(Name = "sender_id")]
            //[JsonPropertyName("sender_id")]
            public string SenderID;

            [DataMember(Name = "message")]
            //[JsonPropertyName("message")]
            public string Message;
        }

        private class TwitchEndpoints
        {
            public const string BaseOAuth = "https://id.twitch.tv";
            public const string BaseAPI = "https://api.twitch.tv";
        }

        private bool IsTwitchChannel;

        private static TwitchContextListing GlobalData;

        // both the twitch channel module and the mirror channel module will have the same context
        TwitchContext ActiveContext;

        static TwitchTokenResponseStore Tokens;

        const string TokenPath = "TwitchTokens.pls";

        string RoomID = "";
        static string ModID = "";

        bool MirrorWriteToTwitchEnabled = true;
        bool MirrorReadFromTwitchEnabled = true;

        private delegate void MirrorMessageDelegate(string message);


        static Dictionary<string, PluginChannelMessageDelegate> TwitchMessageEvents;
        static Dictionary<string, PluginChannelMessageDelegate> IRCMirrorMessageEvents;
        // for sending explicit messages to mirror channel from twitch channel module
        static Dictionary<string, MirrorMessageDelegate> IRCMirrorChannelTable;

        private string MostRecentlySentMessage;

        // this is instantiated as case insensitive
        Dictionary<string, string> UserIDCache;

        const string MirrorCommandHelp = "Enable or disable read and write mirrors. Both are set if neither read or write is passed. Params are [(read|write)] (on|off|enable|disable)";
        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("TwitchFattyAuthURL", GenTwitchAuthTokenURL, "Generates a twitch Oath url for fatty"));
            Commands.Add(new UserCommand("TwitchUserAuthURL", GenTwitchUserAuthTokenURL, "Generates a twitch Oath url a user"));
            Commands.Add(new UserCommand("TwitchRefreshToken", RefreshOAuthTokenCommand, "Refreshes OAuth Tokens. Pass in an access token as argument to validate, otherwise last refresh token is used."));
            Commands.Add(new UserCommand("TwitchMirror", MirrorCommand, MirrorCommandHelp));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("TwitchFattyAuthURL");
            CommandNames.Add("TwitchUserAuthURL"); 
            CommandNames.Add("TwitchRefreshToken");
            CommandNames.Add("TwitchMirror");
        }

        public static string GetAuthScopesString()
        {
            return string.Join(" " ,GlobalData.BotScopes);
        }

        public static string GetUserAuthScopesString()
        {
            return string.Join(" ", GlobalData.UserScopes);
        }

        private string GetMirrorKey(string server, string channel)
        {
            return $"{server}/{channel}";
        }

        private string GetMirrorKey()
        {
            return GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName);
        }

        private void RefreshOAuthToken(TwitchContextListing globals, int tokenIndex, bool triggeredManually, string? newAccessToken  = null)
        {
            var formData = new Dictionary<string, string>();

            if (newAccessToken == null)
            {
                string refreshToken = Tokens.TokenResponses[tokenIndex].RefreshToken;
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
                HttpResponseMessage result = FattyHelpers.HttpRequest(TwitchEndpoints.BaseOAuth, "oauth2/token", HttpMethod.Post, null, null, formData).Result;

                if (result.IsSuccessStatusCode)
                {
                    string returnResult = result.Content.ReadAsStringAsync().Result;
                    TwitchTokenResponse newResponse = FattyHelpers.DeserializeFromJsonString<TwitchTokenResponse>(returnResult);
                    newResponse.IRCUsername = Tokens.TokenResponses[tokenIndex].IRCUsername;
                    newResponse.TwitchUsername = Tokens.TokenResponses[tokenIndex].TwitchUsername;
                    Tokens.TokenResponses[tokenIndex] = newResponse;
                    FattyHelpers.JsonSerializeToPath(Tokens, TokenPath);
                    DateTime Expiration = DateTime.Now + TimeSpan.FromSeconds(Tokens.TokenResponses[tokenIndex].AccessExpiresInSeconds);

                    //OwningChannel.SetServerPassword($"oauth:{newResponse.AccessToken}");
                    if (triggeredManually)
                    {
                        OwningChannel.SendChannelMessage($"Sucessfully refreshed auth token. It will expire at {Expiration.ToString()}. {(newAccessToken != null && newResponse.IRCUsername == null ? "Don't forget to update twitch config." : "")}");
                    }
                }
                else
                {
                    Fatty.PrintWarningToScreen($"Failed to refresh twitch auth token: {result.ReasonPhrase}");
                    if (triggeredManually)
                    {
                        OwningChannel.SendChannelMessage($"Failed to refresh twitch auth token: {result.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                Fatty.PrintWarningToScreen(ex);
            }
        }

        private async void StartValidateOAuthTokenThread()
        {
            while (true)
            {
                try
                {
                    try
                    {
                        for (int i = 0; i < Tokens.TokenResponses.Length; ++i)
                        {
                            TwitchTokenResponse currentToken = Tokens.TokenResponses[i];
                            var headers = FattyHelpers.CreateHTTPRequestHeaders();
                            headers.Add("Authorization", $"OAuth {currentToken.AccessToken}");

                            HttpResponseMessage result = await FattyHelpers.HttpRequest(TwitchEndpoints.BaseOAuth, "oauth2/validate", HttpMethod.Get, null, headers);

                            if (!result.IsSuccessStatusCode)
                            {
                                Fatty.PrintWarningToScreen($"Twitch failed to validate {currentToken.TwitchUsername} \"{result.Content.ReadAsStringAsync().Result}\"");
                            }

                            RefreshOAuthToken(GlobalData, i, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Fatty.PrintWarningToScreen(ex);
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
            // todo: make sure authenticated user

            int userIndex = Tokens.GetIndexByIRCUser(ircUser);

            if (userIndex == -1)
            {
                OwningChannel.SendChannelMessage("sry, don't know who you are");
                return;
            }

            string[] segments = message.Split(" ");
            string newAccessToken = null;
            if (segments.Length == 2)
            {
                newAccessToken = segments[1];
            }
            else if (segments.Length == 3)
            {
                userIndex = Tokens.GetFattyTokenIndex();
                newAccessToken = segments[2];
            }

            RefreshOAuthToken(GlobalData, userIndex, true, newAccessToken);
        }

        private void MirrorCommand(string ircUser, string ircChannel, string message)
        {
            string[] parts = message.Split(' ');

            if (parts.Length > 1)
            {
                if (parts[1].Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    OwningChannel.SendChannelMessage(MirrorCommandHelp);
                    return;
                }

                int partLen = parts.Length;
                string[] enableStrings = { "on", "enable", "1"};
                string[] disableStrings = { "off", "disable", "0" };
                bool enabled = enableStrings.Contains(parts[partLen-1], StringComparer.OrdinalIgnoreCase);
                bool disabled = disableStrings.Contains(parts[partLen-1], StringComparer.OrdinalIgnoreCase);

                if (!(enabled || disabled))
                {
                    OwningChannel.SendChannelMessage("not enabled or disabled");
                    return;
                }

                if (partLen == 3)
                {
                    if (parts[1].Equals("read", StringComparison.OrdinalIgnoreCase))
                    {
                        // if we get this far then it's disabled if it's not enabled.
                        MirrorReadFromTwitchEnabled = enabled;
                        OwningChannel.SendChannelMessage($"read {(enabled ? "enabled" : "disabled")}");
                        return;
                    }
                    else if (parts[1].Equals("write", StringComparison.OrdinalIgnoreCase))
                    {
                        MirrorWriteToTwitchEnabled = enabled;
                        OwningChannel.SendChannelMessage($"write {(enabled ? "enabled" : "disabled")}");
                        return;
                    }
                }
                else
                {
                    MirrorReadFromTwitchEnabled = enabled;
                    MirrorWriteToTwitchEnabled = enabled;
                    OwningChannel.SendChannelMessage($"read and write {(enabled ? "enabled" : "disabled")}");
                    return;
                }
            }

            OwningChannel.SendChannelMessage("I don't get it");
        }

        static public string GetTwitchServerPassword()
        {
            TwitchTokenResponse[] responses = ReadLastAuthTokenResponse().TokenResponses;

            for(int i = 0; i < responses.Length; ++i)
            {
                if (responses[i].TwitchUsername == null)
                {
                    return $"oauth:{responses[i].AccessToken}";
                }
            }
            return "";
        }

        static public TwitchTokenResponseStore ReadLastAuthTokenResponse()
        {
            return FattyHelpers.DeserializeFromPath<TwitchTokenResponseStore>(TokenPath);
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
            IsTwitchChannel = OwningChannel.ServerName == "twitch";

            UserIDCache = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (GlobalData == null)
            {
                TwitchMessageEvents = new Dictionary<string, PluginChannelMessageDelegate>();
                IRCMirrorMessageEvents = new Dictionary<string, PluginChannelMessageDelegate>();
                IRCMirrorChannelTable = new Dictionary<string, MirrorMessageDelegate>();

                GlobalData = FattyHelpers.DeserializeFromPath<TwitchContextListing>("Twitch.cfg");

                Tokens = ReadLastAuthTokenResponse();

                StartValidateOAuthTokenThread();

                var infoTask = GetUserInfo(GlobalData.BotAccountName);
                infoTask.Wait();
                if (infoTask.Result != null)
                {
                    ModID = infoTask.Result.Id;
                }
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
            OwningChannel.UserstateEvent += OnUserstate;
            OwningChannel.UserNoticeEvent += OnUserNotice;

            if (IsMirrorConfigured())
            {
                if (IsTwitchChannel)
                {
                    IRCMirrorMessageEvents[GetMirrorKey("twitch", ActiveContext.TwitchChatChannel)] = OnMirrorChannelIRCMessage;
                }
                else
                {
                    TwitchMessageEvents[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)] = OnMirrorTwitchMessage;
                    IRCMirrorChannelTable[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)] = OnSendMirrorChannelMessage;
                }
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

        private void OnUserstate(UserStateType type, Dictionary<string, string>? tags, string channel, string username)
        {
            if (type == UserStateType.Global || type == UserStateType.User)
            {
                string userType;
                if (IsTwitchBigwigType(tags, out userType))
                {
                    SendMirrorChannelMessage($"FOUND IMPORTANT USER: {userType} - {username}");
                }
            }
            else if (type == UserStateType.Room)
            {
                RoomID = tags["room-id"];
            }
        }

        private void OnUserNotice(Dictionary<string, string>? tags, string channel, string username)
        {
            if (tags != null)
            {
                bool isRaidNotice = false;
                string messageID;
                if (tags.TryGetValue("msg-id", out messageID))
                {
                    if(messageID.Equals("raid", StringComparison.OrdinalIgnoreCase)) 
                    {
                        isRaidNotice = true;
                    }
                }
                    

                string systemMessage;
                if (tags.TryGetValue("system-msg", out systemMessage))
                {
                    systemMessage = systemMessage.Replace(@"\s", " ");
                    SendMirrorChannelMessage(systemMessage);

                    if (isRaidNotice && ActiveContext.ShouldWelcomeRaiders)
                    {
                        // extract the number of raiders from the message. Immitate aeris if there are only one or two raiders
                        int raiderCount = int.Parse(systemMessage.Substring(0,systemMessage.IndexOf(" ")));
                        if (raiderCount < 3)
                        {
                            WelcomeRaidersLikeAeris(tags);
                        }
                    }
                }
            }   
        }

        private async void WelcomeRaidersLikeAeris(Dictionary<string, string> tags)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                string RaidingUser = tags["login"];
                OwningChannel.SendChannelMessage($"Thanks for bringing all of your friends, @{RaidingUser}!");
            }
            catch (Exception e) 
            {
                Fatty.PrintWarningToScreen(e);
            }
        }

        private void OnChannelMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {

            if (IsTwitchChannel)
            {
                if (IsMirrorConfigured())
                {
                    // don't mirror our own messages back to irc
                    if (message != MostRecentlySentMessage)
                    {
                        TwitchMessageEvents[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)].Invoke(tags, ircUser, message);
                    }
                }
                ProcessUserMessage(tags, ircUser, message);
            }
            else
            {
                if (IsMirrorConfigured())
                {
                    if (MirrorWriteToTwitchEnabled)
                    {
                        IRCMirrorMessageEvents[GetMirrorKey("twitch", ActiveContext.TwitchChatChannel)].Invoke(tags, ircUser, message);
                    }
                }
            }
        }

        // sends a message to the mirror channel, useful for messages that we don't want to be visible from twitch chat
        private void SendMirrorChannelMessage(string message)
        {
            if (IsTwitchChannel)
            {
                if(IsMirrorConfigured())
                {
                    IRCMirrorChannelTable[GetMirrorKey()](message);
                }
            }
            else
            {
                throw new Exception("Tried to send mirror message from mirror channel");
            }
        }

        private bool IsTwitchBigwigType(Dictionary<string, string>? tags, out string userType)
        {
            if(tags.TryGetValue("user-type", out userType))
            {
                return userType != "mod" && userType != "";
            }
            
            return false;
        }

        private void ProcessUserMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {
            if (tags != null)
            {
                string firstMessage;
                if (tags.TryGetValue("first-msg", out firstMessage))
                {
                    if (firstMessage == "1")
                    {
                        FilterNewChatterMessages(tags, ircUser, message);
                    }
                }
                UserIDCache[ircUser] = tags["user-id"];
            }
        }

        private void FilterNewChatterMessages(Dictionary<string, string>? tags, string ircUser, string message)
        {
            string strippedMessage = message.RemoveDiacritics().RemoveWhitespace();
            
            string matchedString = GlobalData.BannedPhrases.FirstOrDefault(s => strippedMessage.Contains(s, StringComparison.OrdinalIgnoreCase));
            if (matchedString != null)
            {
                //OwningChannel.SendChannelMessage($"STFU, {matchedString} ass bot");
                string roomID = tags["room-id"];
                string userID = tags["user-id"];

                BanUser(roomID, userID);
            }
        }

        // used to mirror irc messages on twitch
        private void OnMirrorChannelIRCMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {
            // if we're actually trying to mirror. maybe check if host is streaming.
            if (!message.StartsWith(OwningChannel.CommandPrefix))
            {
                TryMirrorUserMessageToTwitch(ircUser, message);
            }
        }

        private async void TryMirrorUserMessageToTwitch(string ircUser, string message)
        {
            try
            {
                int tokenIndex = Tokens.GetIndexByIRCUser(ircUser);
                if (tokenIndex != -1)
                {
                    string userAccessToken = Tokens.TokenResponses[tokenIndex].AccessToken;
                    string twitchUserName = Tokens.TokenResponses[tokenIndex].TwitchUsername;
                    string twitchUserID;
                    if (!UserIDCache.TryGetValue(twitchUserName, out twitchUserID))
                    {
                        TwitchUser user = await GetUserInfo(twitchUserName);
                        twitchUserID = user.Id;
                        UserIDCache[twitchUserName] = user.Id;
                    }
                    TwitchChatSendMessageAsUser(twitchUserID, userAccessToken, message);
                }
            }
            catch (Exception ex) 
            {
                Fatty.PrintWarningToScreen(ex);
            } 
        }

        // used to mirror twitch messages onto irc
        private void OnMirrorTwitchMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {
            if (MirrorReadFromTwitchEnabled)
            {
                string colorString = string.Empty;
                int colorIndex = 99;
                if (tags != null)
                {
                    tags.TryGetValue("color", out colorString);
                    colorIndex = Colors.GetNearestIrcColor(colorString);

                    // assign random color based on name if none provided
                    if (colorIndex == -1)
                    {
                        colorIndex = ircUser.GetHashCode() % 99;
                    }
                }

                OwningChannel.SendChannelMessage($"{(colorIndex != -1 ? $"\x3{colorIndex}" : "")}<{ircUser}>\x0F {message}");
            }
        }


        private void OnSendMirrorChannelMessage(string message)
        {
            OwningChannel.SendChannelMessage(message);
        }

        private void GenTwitchAuthTokenURL(string ircUser, string ircChannel, string message)
        {
            UriBuilder uriBuilder = new UriBuilder(TwitchEndpoints.BaseOAuth)
            {
                Path = "oauth2/authorize"
            };
            NameValueCollection queryData = HttpUtility.ParseQueryString(uriBuilder.Query);
            queryData["client_id"] = GlobalData.ClientID;
            queryData["redirect_uri"] = GlobalData.RedirectURI;
            //queryData["force_verify"] = "true"; 
            queryData["response_type"] = "code";
            queryData["scope"] = GetAuthScopesString();
            queryData["state"] = Guid.NewGuid().ToString("N");
            uriBuilder.Query = queryData.ToString();

            OwningChannel.SendChannelMessage(uriBuilder.Uri.ToString());
        }

        private void GenTwitchUserAuthTokenURL(string ircUser, string ircChannel, string message)
        {
            UriBuilder uriBuilder = new UriBuilder(TwitchEndpoints.BaseOAuth)
            {
                Path = "oauth2/authorize"
            };

            NameValueCollection queryData = HttpUtility.ParseQueryString(uriBuilder.Query);

            queryData["client_id"] = GlobalData.ClientID;
            queryData["redirect_uri"] = GlobalData.RedirectURI;
            //queryData["force_verify"] = "true"; 
            queryData["response_type"] = "code";
            queryData["scope"] = GetUserAuthScopesString();
            queryData["state"] = Guid.NewGuid().ToString("N");
            uriBuilder.Query = queryData.ToString();

            OwningChannel.SendChannelMessage(uriBuilder.Uri.ToString());
        }

        private async Task<TwitchUser> GetUserInfo(string username)
        {
            var headers = GetCommonTwitchRequestHeaders();

            NameValueCollection nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("login", username);
            HttpResponseMessage result = await FattyHelpers.HttpRequest(TwitchEndpoints.BaseAPI, "helix/users", HttpMethod.Get, nameValueCollection, headers);
            if (result.IsSuccessStatusCode)
            {
                TwitchUserResponse user = FattyHelpers.DeserializeFromJsonString<TwitchUserResponse>(result.Content.ReadAsStringAsync().Result);
                return user.Data[0];
            }
            else
            {
                int fattyIndex = Tokens.GetFattyTokenIndex();
                RefreshOAuthToken(GlobalData, fattyIndex, false);
                await Task.Delay(1000);
                HttpResponseMessage retryResult = await FattyHelpers.HttpRequest(TwitchEndpoints.BaseAPI, "helix/users", HttpMethod.Get, nameValueCollection, headers);
                if (retryResult.IsSuccessStatusCode)
                {
                    TwitchUserResponse user = FattyHelpers.DeserializeFromJsonString<TwitchUserResponse>(result.Content.ReadAsStringAsync().Result);
                    return user.Data[0];
                }
                else
                {
                    return null;
                }
            }
        }

        private bool BanUser(string roomID, string userID)
        {
            var headers = GetCommonTwitchRequestHeaders();

            NameValueCollection queryParams = new NameValueCollection()
            {
                {"broadcaster_id", roomID },
                {"moderator_id", ModID }
            };

            string BanReqestBody = CreateBanRequestJson(userID, null, "Bot Spam");

            StringContent content = new StringContent(BanReqestBody, Encoding.UTF8, "application/json");

            var result = FattyHelpers.HttpRequest(TwitchEndpoints.BaseAPI, "helix/moderation/bans", HttpMethod.Post, queryParams, headers, content).Result;

            bool success;

            if (result.IsSuccessStatusCode)
            {
                Fatty.PrintToScreen($"Banned UserID {userID} from RoomId {roomID}");
                success = true;
            }
            else
            {
                string phrase = result.ReasonPhrase;
                string other = result.Content.ReadAsStringAsync().Result;
                Fatty.PrintWarningToScreen($"{phrase} - {other}");
                success = false;
            }    

            return success;
        }

        public static string CreateBanRequestJson(string userId, int? duration, string reason)
        {
            var banRequest = new BanRequestWrapper
            {
                Data = new BanRequest
                {
                    UserId = userId,
                    Duration = duration,
                    Reason = reason
                }
            };

            string json = JsonSerializer.Serialize(banRequest, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            return json;
        }

        private HttpRequestHeaders GetCommonTwitchRequestHeaders()
        {
            HttpRequestHeaders headers = FattyHelpers.CreateHTTPRequestHeaders();
            headers.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.TokenResponses[Tokens.GetFattyTokenIndex()].AccessToken);
            headers.Add("Client-Id", GlobalData.ClientID);
            return headers;
        }

        private bool IsMirrorConfigured()
        {
            return ActiveContext.IRCMirrorServerName != null && ActiveContext.IRCMirrorChannelName != null;
        }

        private async void TwitchChatSendMessageAsUser(string senderID, string senderAccessToken, string message)
        {
            HttpRequestHeaders headers = FattyHelpers.CreateHTTPRequestHeaders();
            headers.Authorization = new AuthenticationHeaderValue("Bearer", senderAccessToken);
            headers.Add("Client-Id", GlobalData.ClientID);

            SendMessageRequest request = new SendMessageRequest
            {
                BroadcasterID = RoomID,
                Message = message,
                SenderID = senderID
            };


            string json = FattyHelpers.JsonSerializeFromObject(request);

            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            // we store this before we send in to prevent a race condition
            MostRecentlySentMessage = message;

            var result = await FattyHelpers.HttpRequest(TwitchEndpoints.BaseAPI, "helix/chat/messages", HttpMethod.Post, null, headers, content);

            if(!result.IsSuccessStatusCode)
            {
                //SendMirrorChannelMessage("Failed to send message");
                string phrase = result.ReasonPhrase;
                string other = result.Content.ReadAsStringAsync().Result;
                SendMirrorChannelMessage($"{phrase} - {other}");
            }
        }
    }
}
