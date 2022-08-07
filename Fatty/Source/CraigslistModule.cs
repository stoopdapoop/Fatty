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

            [DataMember]
            public string SearchString;
        }

        static private Object StaticLock = new object();

        static private CraigslistContextList Contexts;
        private System.Timers.Timer PollTimer;

        bool Muted;

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("craigslist");
        }

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("craigstlist", CraigslistCommand, "call with 'mute' or 'unmute' to silence and resume craiglist reading"));
        }

        public override void PostConnectionModuleInit()
        {
            base.PostConnectionModuleInit();
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);

            // lazy init configuration file
            if (Contexts == null)
            {
                Contexts = FattyHelpers.DeserializeFromPath<CraigslistContextList>("Craigslist.cfg");

                PollTimerElapsed(this, null);
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

        async void PollTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // check each query
            while (true)
            {
                try
                {
                    var client = new CraigslistStreamingClient();

                    var request = new SearchForSaleRequest("sfbay", SearchForSaleRequest.Categories.All)
                    {
                        SearchText = "",
                    };

                    await foreach (var posting in client.StreamPostings(request))
                    {
                        Fatty.PrintToScreen($"{posting.Price} : {posting.Title} - {posting.PostingUrl}");
                    }
                }
                catch (Exception ex)
                {
                    Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                }
            }
        }
    }
}
