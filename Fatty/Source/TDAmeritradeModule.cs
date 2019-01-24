using System;
using System.Runtime.Serialization;
using System.Web;
using RestSharp;

namespace Fatty
{
    class TDAmeritradeModule : FattyModule
    {
        [DataContract]
        class TDAmeritradeConfig
        {
            [DataMember(IsRequired = true)]
            public string ClientID { get; private set; }

            [DataMember(IsRequired = true)]
            public string RedirectURI { get; private set; }
        }

        [DataContract]
        class PostAccessTokenResponse
        {
            [DataMember(Name = "access_token")]
            public string AccessToken { get; private set; }

            [DataMember(Name = "refresh_token")]
            public string RefreshToken { get; private set; }

            [DataMember(Name = "expires_in")]
            public int AccessExpiresInSeconds { get; private set; }

            [DataMember(Name = "refresh_token_expires_in")]
            public int RefreshExpiresInSeconds { get; private set; }

            [DataMember]
            public DateTime AccessExpirationDateTime { get; private set; }

            private void RefreshAccessExpirationTime()
            {
                AccessExpirationDateTime = DateTime.Now.AddSeconds(AccessExpiresInSeconds - Math.Min(AccessExpiresInSeconds, TimeSpan.FromMinutes(5).Seconds));
            }
        }

        TDAmeritradeConfig Config;
        bool InitSuccess = false;

        // todo: refresh tokens periodically, like twice an accessexpirationdate

        const string PostAccessTokenURL = @"https://api.tdameritrade.com/v1/oauth2/token";

        public override void Init(ChannelContext channel)
        {
            base.Init(channel);

            channel.ChannelJoinedEvent += OnChannelJoin;

            Config = FattyHelpers.DeserializeFromPath<TDAmeritradeConfig>("TDAmeritrade.cfg");
            PostAccessTokenResponse oldResponse = FattyHelpers.DeserializeFromPath<PostAccessTokenResponse>("AmeritradeTokens.pls");

            //string code = HttpUtility.UrlDecode("");
            RestClient client = new RestClient(PostAccessTokenURL);

            RestRequest request = new RestRequest(Method.POST);
            request.AddParameter("grant_type", "refresh_token");
            request.AddParameter("refresh_token", oldResponse.RefreshToken);
            request.AddParameter("access_type", "offline");
            request.AddParameter("client_id", Config.ClientID);
            request.AddParameter("redirect_uri", Config.RedirectURI);

            IRestResponse response = client.Execute(request);
            if(response.IsSuccessful)
            {
                Fatty.PrintToScreen("Ameritrade Authentication Sucessful");
                PostAccessTokenResponse result = FattyHelpers.DeserializeFromJsonString<PostAccessTokenResponse>(response.Content);
                InitSuccess = true;
                System.IO.File.WriteAllText(@"AmeritradeTokens.pls", response.Content);
            }
            else
            {
                Fatty.PrintToScreen("Ameritrade Authentication Failed");
                InitSuccess = false;
            }
        }

        private void OnChannelJoin(string ircChannel)
        {
            OwningChannel.SendChannelMessage(InitSuccess ? "TD Ameritrade authenticated fine" : "TD Ameritrade didn't authenticate fine");
        }
    }
}
