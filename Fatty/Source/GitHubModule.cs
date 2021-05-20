using RestSharp;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Timers;

namespace Fatty
{
    public class GitHubModule : FattyModule
    {

        [DataContract]
        public class GitHubContextListing
        {
            [DataMember(IsRequired = true, Name = "GitHubContexts")]
            public List<GitHubContext> AllContexts;
        }

        [DataContract]
        public class GitHubContext
        {
            [DataMember(IsRequired = true)]
            public string ServerName;

            [DataMember(IsRequired = true)]
            public string ChannelName;

            [DataMember(IsRequired = true)]
            public string ProjectEndpoint;

            [IgnoreDataMember]
            public DateTime LastSeen;

            [IgnoreDataMember]
            public bool IsValidEndpoint;
        }

        [DataContract]
        public class GitHubEvent
        {
            [DataMember(Name = "type")]
            public string EventType;

            [DataMember(Name = "actor")]
            public GitHubActor Actor;

            [DataMember(Name = "created_at")]
            public DateTime CreatedDateTime;
        }

        static DataContractJsonSerializerSettings SerializerSettings;


        [DataContract]
        public class GitHubActor
        {
            [DataMember(Name = "display_login")]
            public string DisplayName;

            [DataMember(Name = "url")]
            public string URL;
        }

        private List<GitHubContext> ActiveChannelContexts;
        private Timer PollTimer;

        public GitHubModule()
        {
            ActiveChannelContexts = new List<GitHubContext>();

            // github uses iso 8601 which throws the default serializer settings for a loop
            if (SerializerSettings == null)
            {
                SerializerSettings = new DataContractJsonSerializerSettings();
                SerializerSettings.DateTimeFormat = new DateTimeFormat("yyyy-MM-ddTHH:mm:ssZ");
            }
        }

        public override void GetAvailableCommands(ref List<UserCommand> Commands)
        {
            
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);

            Action<IRestResponse> responseCallback = r => {
                if (r.Request is RestRequest)
                {
                    RestRequest owningRequest = (RestRequest)r.Request;
                    GitHubContext owningContext = (GitHubContext)owningRequest.UserState;

                    List<GitHubEvent> LatestEvents = FattyHelpers.DeserializeFromJsonString<List<GitHubEvent>>(r.Content, SerializerSettings);
                    if (LatestEvents.Count > 0)
                    {
                        owningContext.LastSeen = LatestEvents[0].CreatedDateTime;
                        owningContext.IsValidEndpoint = true;
                    }
                }
            };

            GitHubContextListing  contextListing = FattyHelpers.DeserializeFromPath<GitHubContextListing>("GitHub.cfg");

            // only care about channels that this channel is looking at
            foreach(GitHubContext ghContext in contextListing.AllContexts)
            {
                if(ghContext.ServerName == OwningChannel.ServerName && ghContext.ChannelName == OwningChannel.ChannelName)
                {
                    ActiveChannelContexts.Add(ghContext);
                }
            }


            foreach (GitHubContext ghContext in ActiveChannelContexts)
            {
                RestClient client = new RestClient(ghContext.ProjectEndpoint);

                RestRequest request = new RestRequest("events");
                request.UserState = ghContext;

                client.ExecuteAsync(request, responseCallback);
            }

            PollTimer = new Timer(TimeSpan.FromSeconds(30.0).TotalMilliseconds);
            PollTimer.Elapsed += PollTimerElapsed;
            PollTimer.AutoReset = true;
            PollTimer.Start();
        }


        void PollTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Action<IRestResponse> responseCallback = r => {
                if (r.Request is RestRequest)
                {
                    RestRequest owningRequest = (RestRequest)r.Request;
                    GitHubContext owningContext = (GitHubContext)owningRequest.UserState;

                    List<GitHubEvent> LatestEvents = FattyHelpers.DeserializeFromJsonString<List<GitHubEvent>>(r.Content, SerializerSettings);
                    List<GitHubEvent> UnseenEvents = new List<GitHubEvent>();
                    if (LatestEvents.Count > 0)
                    {
                        foreach(GitHubEvent latestEvent in LatestEvents)
                        {
                            if (latestEvent.CreatedDateTime <= owningContext.LastSeen)
                            {
                                break;
                            }
                            if(owningContext.LastSeen < latestEvent.CreatedDateTime)
                            {
                                owningContext.LastSeen = latestEvent.CreatedDateTime;
                            }

                            UnseenEvents.Add(latestEvent);
                        }

                        UnseenEvents.Reverse();
                        EmitEventMessages(UnseenEvents);
                    }
                }
            };

            foreach (GitHubContext ghContext in ActiveChannelContexts)
            {
                if (ghContext.IsValidEndpoint)
                {
                    RestClient client = new RestClient(ghContext.ProjectEndpoint);

                    RestRequest request = new RestRequest("events");
                    request.UserState = ghContext;

                    client.ExecuteAsync(request, responseCallback);
                }
            }
        }

        void EmitEventMessages(List<GitHubEvent> events)
        {
            foreach (GitHubEvent unseen in events)
            {
                OwningChannel.SendChannelMessage($"new {unseen.EventType} from {unseen.Actor.DisplayName}. More detailed info to come later.");
            }
        }
    }
}
