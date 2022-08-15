using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using DotnetCraigslist;
using System.Timers;

namespace Fatty
{
    class CraigslistModule : FattyModule
    {

        [DataContract]
        public class CraigslistContextList
        {
            [DataMember(IsRequired = true, Name = "CraigslistContexts")]
            public List<CraigslistContext> AllContexts;
        }

        [DataContract]
        public class CraigslistContext
        {
            [DataMember(IsRequired = true)]
            public string ServerName;

            [DataMember(IsRequired = true)]
            public string ChannelName;

            [DataMember(IsRequired = true)]
            public List<CraigslistWatch> WatchList;
        }

        [DataContract]
        public class CraigslistWatch
        {
            [DataMember(IsRequired = true)]
            public string RegionName;

            [DataMember(IsRequired = true)]
            public string Category;

            [DataMember]
            public string? SearchString;

            [DataMember]
            public int? MaxPrice;

            [DataMember]
            public int? MinPrice;

            [DataMember]
            public int? MinEngineDisplacement;

            [DataMember]
            public int? MaxEngineDisplacement;

            [DataMember]
            public int? MaxModelYear;

            [DataMember]
            public int? MinModelYear;

            [DataMember]
            public bool CleanTitleOnly;

            [DataMember]
            public bool? IncludeNearbyAreas;

            [DataMember]
            public SearchForSaleRequest.Purveyors? Purveyor;

            [DataMember]
            public List<SearchForSaleRequest.MotorcycleType>? MotorcycleTypes;

           [DataMember]
           public List<string>? ExcludedTerms; 
        }

        private List<CraigslistContext> ActiveContexts;

        bool Muted;

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("craigslist");
        }

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("craigslist", CraigslistCommand, "call with 'mute' or 'unmute' to silence and resume craiglist reading"));
        }

        public override void PostConnectionModuleInit()
        {
            base.PostConnectionModuleInit();
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);

            // lazy init configuration file
            CraigslistContextList Contexts = FattyHelpers.DeserializeFromPath<CraigslistContextList>("Craigslist.cfg");
            if(Contexts != null)
            {
                ActiveContexts = new List<CraigslistContext>();
                foreach (var context in Contexts.AllContexts)
                {
                    if (context.ServerName == OwningChannel.ServerName)
                    {
                        if (context.ChannelName == OwningChannel.ChannelName)
                        {
                            ActiveContexts.Add(context);
                        }
                    }
                }
            }
        }

        private void CraigslistCommand(string ircUser, string ircChannel, string message)
        {
            bool succeeded = false;
            string loweredMessage = message.ToLower();
            if (loweredMessage == "mute")
            {
                Muted = true;
                succeeded = true;
            }
            else if (loweredMessage == "unmute")
            {
                Muted = false;
                succeeded = true;
            }


            if (succeeded)
            {
                OwningChannel.SendChannelMessage(Muted ? "muted" : "unmuted");
            }
            else
            {
                OwningChannel.SendChannelMessage("what?");
            }
        }

        static async void ListenForPosts(CraigslistModule sender)
        {
            // check each query
            while (sender != null)
            {
                try
                {
                    var client = new CraigslistStreamingClient();
                    CraigslistWatch firstWatch = sender.ActiveContexts[0].WatchList[0];

                    var request = new SearchForSaleRequest(firstWatch.RegionName, firstWatch.Category)
                    {
                        SearchText = firstWatch.SearchString,
                        TitleStatuses = firstWatch.CleanTitleOnly == true ? new[] { SearchForSaleRequest.TitleStatus.Clean } : null,
                        MaxPrice = firstWatch.MaxPrice,
                        MinPrice = firstWatch.MinPrice,
                        MinModelYear = firstWatch.MinModelYear,
                        MaxModelYear = firstWatch.MaxModelYear,
                        MinEngineDisplacement = firstWatch.MinEngineDisplacement,
                        MaxEngineDisplacement = firstWatch.MaxEngineDisplacement,
                        IncludeNearbyAreas = (firstWatch.IncludeNearbyAreas != null ? (bool)firstWatch.IncludeNearbyAreas : false),
                        Purveyor = firstWatch.Purveyor == null ? SearchForSaleRequest.Purveyors.All : (SearchForSaleRequest.Purveyors)firstWatch.Purveyor,
                        MotorcycleTypes = firstWatch.MotorcycleTypes,
                    };
                   
                    await foreach (var posting in client.StreamSearchResults(request))
                    {
                        if (!sender.Muted)
                        {
                            if(firstWatch.ExcludedTerms != null)
                            {
                                if(sender.TitleContainsExclusion(posting.Title, firstWatch.ExcludedTerms))
                                {
                                    continue;
                                }
                            }
                            sender.OwningChannel.SendChannelMessage($"{posting.Price} : {posting.Title} - {posting.PostingUrl}");
                            await Task.Delay(750);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                }
            }
            Fatty.PrintWarningToScreen("Craigslist Cleanup occurred");
        }

        private bool TitleContainsExclusion(string title, IEnumerable<string> Exclusions)
        {
            foreach (string exclusion in Exclusions)
            {
                if (title.Contains(exclusion, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        void OnChannelJoined(string ircChannel)
        {
            ListenForPosts(this);
        }

        public override void RegisterEvents()
        {
            OwningChannel.ChannelJoinedEvent += OnChannelJoined;
        }
    }
}
