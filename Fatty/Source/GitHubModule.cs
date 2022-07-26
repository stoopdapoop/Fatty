using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Timers;
using System.Threading;
using System.Text;

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
            
            [DataMember(Name = "ref_type")]
            public string RefType;

            [DataMember(Name = "member")]
            public GitHubMember Member;

            [DataMember(Name = "release")]
            public GitHubRelease Release;
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

        [DataContract]
        public class GitHubMember
        {
            [DataMember(Name = "login")]
            public string Login;

            [DataMember(Name = "name")]
            public string Name;
        }

        [DataContract]
        public class GitHubRelease
        {
            [DataMember(Name = "body")]
            public string Body;

            [DataMember(Name = "html_url")]
            public string URL;
        }

            class FattyRequest : RestRequest
        {
            public FattyRequest(string resource) : base(resource) { }
            public object UserState { get; set; }
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

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);

            Action<IRestResponse> responseCallback = r => {
                if (r.Request is FattyRequest)
                {
                    FattyRequest owningRequest = (FattyRequest)r.Request;
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

                FattyRequest request = new FattyRequest("events");
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
                if (r.Request is FattyRequest)
                {
                    FattyRequest owningRequest = (FattyRequest)r.Request;
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

                    FattyRequest request = new FattyRequest("events");
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
                if (events.Count > 1)
                {
                    Thread.Sleep(500);
                }
            }
        }

        string FormatEventString(GitHubEvent evnt)
        {
            switch(evnt.EventType)
            {
                case "PushEvent":
                    return GetPushEventString(evnt);
                case "IssuesEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} issue \"{evnt.Payload.Issue.IssueTitle}\" - {evnt.Payload.Issue.PageURL}";
                case "IssueCommentEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} comment \"{evnt.Payload.Issue.IssueTitle}\" - {evnt.Payload.Issue.PageURL} // {evnt.Payload.Comment.Body}";
                case "PullRequestEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} pull request for {evnt.Repo.RepoName}";
                case "DeleteEvent":
                    return $"{evnt.Actor.DisplayName} Deleted {evnt.Payload.RefType} from {evnt.Repo.RepoName}";
                case "CommitCommentEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} commit comment in {evnt.Repo.RepoName}";
                case "CreateEvent":
                    return $"{evnt.Actor.DisplayName} created {evnt.Payload.RefType} in {evnt.Repo.RepoName}";
                case "ForkEvent":
                    return $"{evnt.Actor.DisplayName} forked {evnt.Repo.RepoName}";
                case "MemberEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} member {evnt.Payload.Member.Name} \"{evnt.Payload.Member.Login}\" to {evnt.Repo.RepoName}";
                case "PublicEvent":
                    return $"{evnt.Actor.DisplayName} made {evnt.Repo.RepoName} public";
                case "WatchEvent":
                    return $"{evnt.Actor.DisplayName} started watching {evnt.Repo.RepoName}!!";
                case "ReleaseEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} release in {evnt.Repo.RepoName}. \"{evnt.Payload.Release.Body}\" -- {evnt.Payload.Release.URL}";
                default:
                    return $"Unhandled Event \"{evnt.EventType}\" Triggered by {evnt.Actor.DisplayName}! Fix or ignore.";
            }
        }

        string GetPushEventString(GitHubEvent evnt)
        {
            int commitCount = evnt.Payload.PayloadSize;
            StringBuilder messageAccumulator = new StringBuilder();

            messageAccumulator.Append($"{evnt.Actor.DisplayName} pushed {evnt.Payload.PayloadSize} commits to {evnt.Repo.RepoName}: ");
            for (int i = 0; i < commitCount; ++i)
            {
                string commitURL = $"https://www.github.com/{evnt.Repo.RepoName}/commit/{evnt.Payload.Commits[i].Hash.Substring(0, 8)}";
                messageAccumulator.Append($"\"{evnt.Payload.Commits[i].Message}\" - {commitURL}");
                if(i != commitCount - 1)
                {
                    messageAccumulator.Append(" || ");
                }
            }

            return messageAccumulator.ToString();
        }
    }
}
