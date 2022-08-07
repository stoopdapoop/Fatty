using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Net.Http;
using System.Web;

namespace Fatty
{
    class GoogleModule : FattyModule
    {

        private static GoogleConfig Config;


        #region GoogleStructs

        [DataContract]
        private class GoogleConfig
        {
            [DataMember(IsRequired = true)]
            public string GoogleAPIKey { get; private set; }
            [DataMember(IsRequired = true)]
            public string GoogleCustomSearchID { get; private set; }
        }

        [DataContract]
        private class GoogleSearchItem
        {
            [DataMember]
            public string kind { get; set; }
            [DataMember]
            public string title { get; set; }
            [DataMember]
            public string link { get; set; }
            [DataMember]
            public string displayLink { get; set; }
        }

        [DataContract]
        private class SourceUrl
        {
            [DataMember]
            public string type { get; set; }
            [DataMember]
            public string template { get; set; }
        }

        [DataContract]
        private class GoogleSearchResults
        {
            [DataMember]
            public string kind { get; set; }
            [DataMember]
            public SourceUrl url { get; set; }
            [DataMember]
            public GoogleSearchItem[] items { get; set; }
        }
        #endregion

        #region Private Methods

        #endregion

        #region Public Commands

        public GoogleModule()
        {
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("g");
            CommandNames.Add("gis");
        }

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("g", Google, "google search"));
            Commands.Add(new UserCommand("gis", GoogleImageSearch, "google image search"));
        }

        public override void PostConnectionModuleInit()
        {
            base.PostConnectionModuleInit();

            if (Config == null)
            {
                Config = FattyHelpers.DeserializeFromPath<GoogleConfig>("Google.cfg");
            }
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
        }

        public void Google(string ircUser, string ircChannel, string message)
        {
            string argument = HttpUtility.UrlEncode(FattyHelpers.RemoveCommandName(message));
            string searchURL = $"https://www.googleapis.com/customsearch/v1?key={Config.GoogleAPIKey}&cx={Config.GoogleCustomSearchID}&q={argument}";
            GoogleAPIPrinter(searchURL);
        }

        public void GoogleImageSearch(string ircUser, string ircChannel, string message)
        {
            string argument = HttpUtility.UrlEncode(FattyHelpers.RemoveCommandName(message));
            string searchURL = $"https://www.googleapis.com/customsearch/v1?key={Config.GoogleAPIKey}&cx={Config.GoogleCustomSearchID}&searchType=image&q={argument}";
            GoogleAPIPrinter(searchURL);
        }
        #endregion

        #region utils
        private void GoogleAPIPrinter(string searchURL)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(20);
            var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, searchURL));

            StreamReader reader = new StreamReader(response.Content.ReadAsStream());
            String data = reader.ReadToEnd();

            GoogleSearchResults results = FattyHelpers.DeserializeFromJsonString<GoogleSearchResults>(data);
            StringBuilder messageAccumulator = new StringBuilder();
            int i = 0;
            int messageOverhead = FattyHelpers.GetMessageOverhead(OwningChannel.ChannelName);
            while (i < 10 && results.items != null && i < results.items.Length)
            {
                if (results.items.Length >= i)
                {
                    StringBuilder resultAccumulator = new StringBuilder();
                    GoogleSearchItem resultIterator = results.items[i++];
                    resultAccumulator.Append(String.Format("\"{0}\"", resultIterator.title));
                    resultAccumulator.Append(" - ");
                    resultAccumulator.Append(String.Format("\x02{0}\x02", resultIterator.link));
                    resultAccumulator.Append(@" 4| ");
                    if (messageAccumulator.Length + resultAccumulator.Length + messageOverhead <= 480)
                        messageAccumulator.Append(resultAccumulator);
                    else
                        break;
                }
                else
                {
                    break;
                }
            }
            if (messageAccumulator.Length == 0)
                OwningChannel.SendChannelMessage("No Results Found");
            else
                OwningChannel.SendChannelMessage(messageAccumulator.ToString());
        }
        #endregion

    }
}
