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
            public string ClientID;

            [DataMember(IsRequired = true)]
            public string RedirectURI;

            [DataMember(IsRequired = true)]
            public string ClientSecret;

            //[DataMember(IsRequired = true)]
            //public string AccessToken;

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

            [DataMember(Name = "scope")]
            string[] TokenScopes { get; set; }

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

            [DataMember(Name = "view_count", IsRequired = false)]
            public int ViewCount { get; set; }

            [DataMember(Name = "email")]
            public string Email { get; set; }
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

        private class TwitchEndpoints
        {
            public const string BaseOAuth = "https://id.twitch.tv";
            public const string BaseAPI = "https://api.twitch.tv";
        }

        private bool IsTwitchChannel;

        private static TwitchContextListing GlobalData;
        TwitchContext ActiveContext;

        static TwitchTokenResponse Tokens;

        const string TokenPath = "TwitchTokens.pls";

        string RoomID = "";
        static string ModID = "";


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

            //TwitchTokenResponse oldValues = FattyHelpers.DeserializeFromPath<TwitchTokenResponse>(TokenPath);

            string refreshToken = Tokens.RefreshToken;

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
                HttpResponseMessage result = FattyHelpers.HttpRequest(TwitchEndpoints.BaseOAuth, "oauth2/token", HttpMethod.Post, null, null, formData).Result;

                if (result.IsSuccessStatusCode)
                {
                    string returnResult = result.Content.ReadAsStringAsync().Result;
                    Tokens = FattyHelpers.DeserializeFromJsonString<TwitchTokenResponse>(returnResult);
                    FattyHelpers.JsonSerializeToPath(Tokens, TokenPath);
                    //GlobalData.AccessToken = oldValues.AccessToken;
                    DateTime Expiration = DateTime.Now + TimeSpan.FromSeconds(Tokens.AccessExpiresInSeconds);
                    //OwningChannel.SendChannelMessage($"Sucessfully refreshed auth token. It will expire at {Expiration.ToString()}. {(newAccessToken != null ? "Don't forget to update twitch config." : "")}");
                }
                else
                {
                    Fatty.PrintWarningToScreen($"Failed to refresh twitch auth token: {result.ReasonPhrase}");
                    //OwningChannel.SendChannelMessage($"Failed to refresh twitch auth token: {result.ReasonPhrase}");
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
                    headers.Add("Authorization", $"OAuth {Tokens.AccessToken}");

                    HttpResponseMessage result = await FattyHelpers.HttpRequest(TwitchEndpoints.BaseOAuth, "oauth2/validate", HttpMethod.Get, null, headers);

                    if (!result.IsSuccessStatusCode)
                    {
                        Fatty.PrintWarningToScreen(result.Content.ReadAsStringAsync().Result);
                    }

                    RefreshOAuthToken(GlobalData);
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

                Tokens = FattyHelpers.DeserializeFromPath<TwitchTokenResponse>(TokenPath);

                ValidateOAuthToken();

                //string modNick = OwningChannel.GetFattyNick();
                string modNick = "RagnaTheMenace";
                var infoTask = GetUserInfo(modNick);
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

            if (IsMirrorConfigured())
            {
                if (IsTwitchChannel)
                {
                    IRCMirrorMessageEvents[GetMirrorKey("twitch", ActiveContext.TwitchChatChannel)] = OnMirrorChannelIRCMessage;
                }
                else
                {
                    TwitchMessageEvents[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)] = OnMirrorTwitchMessage;
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

            }
            else if (type == UserStateType.Room)
            {
                RoomID = tags["room-id"];
            }
        }

        private void OnChannelMessage(Dictionary<string, string>? tags, string ircUser, string message)
        {

            if (IsTwitchChannel)
            {
                if (IsMirrorConfigured())
                {
                    TwitchMessageEvents[GetMirrorKey(ActiveContext.IRCMirrorServerName, ActiveContext.IRCMirrorChannelName)].Invoke(tags, ircUser, message);
                }
                ProcessUserMessage(tags, ircUser, message);
            }
            else
            {
                if (IsMirrorConfigured())
                {
                    IRCMirrorMessageEvents[GetMirrorKey("twitch", ActiveContext.TwitchChatChannel)].Invoke(tags, ircUser, message);
                }
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
                //OwningChannel.SendChannelMessage($"STFU, {matchedString} ass bot");
                string roomID = tags["room-id"];
                string userID = tags["user-id"];

                BanUser(roomID, userID);
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
            UriBuilder uriBuilder = new UriBuilder(TwitchEndpoints.BaseOAuth)
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
                RefreshOAuthToken(GlobalData);
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

            var content = new StringContent(BanReqestBody, Encoding.UTF8, "application/json");

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

            var json = JsonSerializer.Serialize(banRequest, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            return json;
        }

        private HttpRequestHeaders GetCommonTwitchRequestHeaders()
        {
            HttpRequestHeaders headers = FattyHelpers.CreateHTTPRequestHeaders();
            headers.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.AccessToken);
            headers.Add("Client-Id", GlobalData.ClientID);
            return headers;
        }

        private bool IsMirrorConfigured()
        {
            return ActiveContext.IRCMirrorServerName != null && ActiveContext.IRCMirrorChannelName != null;
        }
    }
}
