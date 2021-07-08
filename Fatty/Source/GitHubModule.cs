using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Timers;
using System.Threading;

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

            [DataMember(IsRequired = false)]
            public string AccessToken;

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

            [DataMember(Name = "repo")]
            public GitHubRepo Repo;

            [DataMember(Name = "payload")]
            public GitHubPayload Payload;
            
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

        [DataContract]
        public class GitHubRepo
        {
            [DataMember(Name = "name")]
            public string RepoName;
        }

        [DataContract]
        public class GitHubPayload
        {
            [DataMember(Name = "head")]
            public string Head;

            [DataMember(Name = "size")]
            public int PayloadSize;

            [DataMember(Name = "commits")]
            public List<GitHubCommit> Commits;

            [DataMember(Name = "action")]
            public string ActionName;

            [DataMember(Name = "issue")]
            public GitHubIssue Issue;

            [DataMember(Name = "comment")]
            public GitHubComment Comment;
        }

        [DataContract]
        public class GitHubIssue
        {
            [DataMember(Name = "html_url")]
            public string PageURL;

            [DataMember(Name = "title")]
            public string IssueTitle;
        }

        [DataContract]
        public class GitHubComment
        {
            [DataMember(Name = "body")]
            public string Body;
        }

        [DataContract]
        public class GitHubCommit
        {
            [DataMember(Name = "sha")]
            public string Hash;

            [DataMember(Name = "message")]
            public string Message;

            [DataMember(Name = "url")]
            public string URL;
        }



        private List<GitHubContext> ActiveChannelContexts;
        private System.Timers.Timer PollTimer;

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
                    if (LatestEvents != null && LatestEvents.Count > 0)
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
                var authen = new JwtAuthenticator(ghContext.AccessToken);
                client.Authenticator = authen;

                RestRequest request = new RestRequest("events");
                request.UserState = ghContext;

                client.ExecuteAsync(request, responseCallback);
            }

            PollTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30.0).TotalMilliseconds);
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
                    if (LatestEvents != null && LatestEvents.Count > 0)
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

                    var authen = new JwtAuthenticator(ghContext.AccessToken);
                    client.Authenticator = authen;

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
                OwningChannel.SendChannelMessage(FormatEventString(unseen));
                Thread.Sleep(2000);
            }
        }

        string FormatEventString(GitHubEvent evnt)
        {
            switch(evnt.EventType)
            {
                case "PushEvent":
                    string commitURL = $"https://www.github.com/{evnt.Repo.RepoName}/commit/{evnt.Payload.Head.Substring(0,8)}";
                    return $"{evnt.Actor.DisplayName} pushed {evnt.Payload.PayloadSize} commits to {evnt.Repo.RepoName}: {evnt.Payload.Commits[0].Message} - {commitURL}";
                case "IssuesEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} issue \"{evnt.Payload.Issue.IssueTitle}\" - {evnt.Payload.Issue.PageURL}";
                case "IssueCommentEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} comment \"{evnt.Payload.Issue.IssueTitle}\" - {evnt.Payload.Issue.PageURL} // {evnt.Payload.Comment.Body}";
                default:
                    return $"Unhandled Event \"{evnt.EventType}\" Triggered by {evnt.Actor.DisplayName}! Fix or ignore.";
            }
        }
    }
}
