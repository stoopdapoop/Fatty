using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Timers;
using System.Threading;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Fatty
{
    public class GitHubModule : FattyModule
    {

        #region DataContracts

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
            public bool IsValidEndpoint = true;

            [IgnoreDataMember]
            public Dictionary<string, string> LatestWikiHash;

            [IgnoreDataMember]
            public int PollInterval;

#nullable enable
            [IgnoreDataMember]
            public string? Etag;
#nullable disable

            [OnDeserialized]
            private void DeserializationInitializer(StreamingContext ctx)
            {
                LatestWikiHash = new Dictionary<string, string>();
            }
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

            [DataMember(Name = "pages")]
            public List<GitHubPage> Pages;
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

            [DataMember(Name = "html_url")]
            public string PageURL;
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

        [DataContract]
        public class GitHubPage
        {
            [DataMember(Name = "html_url")]
            public string PageURL;

            [DataMember(Name = "action")]
            public string ActionName;

            [DataMember(Name = "page_name")]
            public string PageName;
            
            [DataMember(Name = "title")]
            public string Title;

            [DataMember(Name = "summary")]
            public string Summary;

            [DataMember(Name = "sha")]
            public string PageHash;
            
        }

        #endregion

        class BatchedRequest : RestRequest
        {
            public BatchedRequest(string resource) : base(resource) { }

            public List<(GitHubContext, Action<IRestResponse, GitHubContext>)> Listeners { get; set; }
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

            GitHubQueryScheduler.InitScheduler();
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

            Action<IRestResponse, GitHubContext> FirstResponseCallback = (r, c) => {
                if (r.Request is BatchedRequest)
                {
                    BatchedRequest owningRequest = (BatchedRequest)r.Request;
                    GitHubContext owningContext = c;

                    if (r.IsSuccessful && owningContext != null)
                    {
                        owningContext.IsValidEndpoint = true;
                        List<GitHubEvent> LatestEvents = FattyHelpers.DeserializeFromJsonString<List<GitHubEvent>>(r.Content, SerializerSettings);
                        if (LatestEvents != null && LatestEvents.Count > 0)
                        {
                            owningContext.LastSeen = LatestEvents[0].CreatedDateTime;

                            foreach(GitHubEvent evnt in LatestEvents)
                            {
                                if(evnt.EventType == "GollumEvent")
                                {
                                    foreach (GitHubPage page in evnt.Payload.Pages)
                                    {
                                        owningContext.LatestWikiHash.TryAdd(page.PageURL, page.PageHash);
                                    }
                                }
                            }
                        }

                        var pollHeader = r.Headers.Single(header => { return header.Name == "X-Poll-Interval"; });
                        int pollInterval = 60;
                        if (pollHeader != null)
                        {
                            if (!int.TryParse((string)r.Headers.Single(header => { return header.Name == "X-Poll-Interval"; }).Value, out pollInterval))
                            {
                                // have to set again, since tryparse zeroes on failure
                                pollInterval = 60;
                            }

                        }
                        owningContext.PollInterval = pollInterval;

                        var ETagHeader = r.Headers.Single(header => { return header.Name == "ETag"; });
#nullable enable
                        string? ETag = null;
                        if (ETagHeader is not null)
                        {
                            ETag = (string?)ETagHeader.Value;
                        }
                        owningContext.Etag = ETag;
#nullable disable
                    }
                    else
                    {
                        Fatty.PrintWarningToScreen($"GitHub first contact error: {r.StatusCode}: {r.StatusDescription} - {r.ErrorMessage}", Environment.StackTrace);

                        if (owningContext != null)
                        {
                            owningContext.IsValidEndpoint = false;
                            Fatty.PrintWarningToScreen($"{owningContext.ProjectEndpoint} in {owningContext.ChannelName} on {owningContext.ServerName}");
                        }
                    }
                }
            };

            GitHubContextListing  contextListing = FattyHelpers.DeserializeFromPath<GitHubContextListing>("GitHub.cfg");

            // only care about channels that this channel is looking at
            foreach(GitHubContext ghContext in contextListing.AllContexts)
            {
                if (ghContext.ServerName == OwningChannel.ServerName && ghContext.ChannelName == OwningChannel.ChannelName)
                {
                    ActiveChannelContexts.Add(ghContext);
                }
            }


            foreach (GitHubContext ghContext in ActiveChannelContexts)
            {
                GitHubQueryScheduler.ScheduleQuery(ghContext, FirstResponseCallback);
            }

            PollTimer = new System.Timers.Timer(TimeSpan.FromSeconds(60).TotalMilliseconds);
            PollTimer.Elapsed += PollTimerElapsed;
            PollTimer.AutoReset = true;
            PollTimer.Start();
        }


        void PollTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Action<IRestResponse, GitHubContext> PollEventsCallback = (r, c) => {
                if (r.Request is BatchedRequest)
                {
                    BatchedRequest owningRequest = (BatchedRequest)r.Request;
                    GitHubContext owningContext = c;
                    
                    if (r.IsSuccessful && owningContext != null)
                    {
                        List<GitHubEvent> LatestEvents = FattyHelpers.DeserializeFromJsonString<List<GitHubEvent>>(r.Content, SerializerSettings);
                        List<GitHubEvent> UnseenEvents = new List<GitHubEvent>();

                        if (LatestEvents != null && LatestEvents.Count > 0)
                        {
                            foreach (GitHubEvent latestEvent in LatestEvents)
                            {
                                if (latestEvent.CreatedDateTime <= owningContext.LastSeen)
                                {
                                    break;
                                }
                                if (owningContext.LastSeen < latestEvent.CreatedDateTime)
                                {
                                    owningContext.LastSeen = latestEvent.CreatedDateTime;
                                }

                                UnseenEvents.Add(latestEvent);
                            }

                            UnseenEvents.Reverse();
                            EmitEventMessages(UnseenEvents, owningContext);

                            UnseenEvents.ForEach(e => PostReportEvent(e, owningContext));
                        }
                    }
                    else
                    {
                        Fatty.PrintWarningToScreen($"GitHub event poll request error: {r.StatusCode}: {r.StatusDescription} - {r.ErrorMessage}", Environment.StackTrace);
                        if (owningContext != null)
                        {
                            Fatty.PrintWarningToScreen($"{owningContext.ProjectEndpoint} in {owningContext.ChannelName} on {owningContext.ServerName}");
                        }
                    }
                }
            };

            try
            {
                foreach (GitHubContext ghContext in ActiveChannelContexts)
                {
                    if (ghContext.IsValidEndpoint)
                    {
                        GitHubQueryScheduler.ScheduleQuery(ghContext, PollEventsCallback);
                    }
                }
            }
            catch (Exception ex) 
            {
                Fatty.PrintWarningToScreen($"Github Issue: {ex.Message}", Environment.StackTrace);
            }
        }

        void EmitEventMessages(List<GitHubEvent> events, GitHubContext context)
        {
            foreach (GitHubEvent unseen in events)
            {
                if (ShouldReportEvent(unseen, context))
                {
                    OwningChannel.SendChannelMessage(FormatEventString(unseen, context));
                    if (events.Count > 1)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        bool ShouldReportEvent(GitHubEvent evnt, GitHubContext context)
        {
            
            return true;
        }

        // function for handling events after they've been reported
        void PostReportEvent(GitHubEvent evnt, GitHubContext context)
        {
            switch (evnt.EventType)
            {
                case "GollumEvent":
                    PostReportGollumEvent(evnt, context);
                    break;
            }
        }

        void PostReportGollumEvent(GitHubEvent evnt, GitHubContext context)
        {
            foreach (var page in evnt.Payload.Pages)
            {
                context.LatestWikiHash[page.PageURL] = page.PageHash;
            }
        }

        string FormatEventString(GitHubEvent evnt, GitHubContext context)
        {
            switch(evnt.EventType)
            {
                case "PushEvent":
                    return FormatPushEventString(evnt);
                case "IssuesEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} issue \"{evnt.Payload.Issue.IssueTitle}\" - {evnt.Payload.Issue.PageURL}";
                case "IssueCommentEvent":
                    return FormatIssueCommentEventString(evnt);
                case "PullRequestEvent":
                    return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} pull request for {evnt.Repo.RepoName}";
                case "DeleteEvent":
                    return $"{evnt.Actor.DisplayName} Deleted {evnt.Payload.RefType} from {evnt.Repo.RepoName}";
                case "CommitCommentEvent":
                    return $"{evnt.Actor.DisplayName} made comment on commit in {evnt.Repo.RepoName} - {evnt.Payload.Comment.PageURL}";
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
                case "GollumEvent":
                    return FormatGollumEventString(evnt, context);

                default:
                    // see "ShouldReportEvent"
                    return $"Unhandled Event \"{evnt.EventType}\" Triggered by {evnt.Actor.DisplayName}! Fix or ignore.";
            }
        }

        string FormatPushEventString(GitHubEvent evnt)
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

        string FormatIssueCommentEventString(GitHubEvent evnt)
        {
            const int previewLength = 26;
            string bodySnippet = evnt.Payload.Comment.Body.Substring(0, Math.Min(evnt.Payload.Comment.Body.Length, previewLength));
            if (evnt.Payload.Comment.Body.Length > previewLength)
                bodySnippet += "...";
            return $"{evnt.Actor.DisplayName} {evnt.Payload.ActionName} comment \"{evnt.Payload.Issue.IssueTitle}\" - {evnt.Payload.Issue.PageURL} // {bodySnippet}";
        }

        string FormatGollumEventString(GitHubEvent evnt, GitHubContext context)
        {
            string ReturnString;
            if (evnt.Payload.Pages.Count > 0)
            {
                GitHubPage firstPage = evnt.Payload.Pages[0];
                string oldHash;
                if (context.LatestWikiHash.TryGetValue(firstPage.PageURL, out oldHash))
                {
                    string compareURL = $"{firstPage.PageURL}/_compare/{oldHash}...{firstPage.PageHash}";
                    ReturnString = $"{evnt.Actor.DisplayName} {firstPage.ActionName} \"{firstPage.Title}\" Wiki page : {compareURL}";
                }
                else
                {
                    // just return the regular url if we haven't seen the old hash and can't make a comparison url
                    ReturnString = $"{evnt.Actor.DisplayName} {evnt.Payload.Pages[0].ActionName} \"{evnt.Payload.Pages[0].Title}\" Wiki page : {firstPage.PageURL}";
                }
            }
            else
            {
                ReturnString = $"{evnt.Actor.DisplayName} made some change to the wiki, but there are no pages associated with the change. This should never happen";
            }

            return ReturnString;
        }

        static public class GitHubQueryScheduler
        {
            private static Mutex mut = new Mutex();
            private static Dictionary<string, List<(GitHubContext, Action<IRestResponse, GitHubContext>)>> GithubQueue = new();
            private static System.Timers.Timer PollTimer = null;

            public static void InitScheduler()
            {
                lock (mut)
                {
                    if (PollTimer == null)
                    {
                        PollTimer = new System.Timers.Timer(2000);
                        PollTimer.Elapsed += ExecuteQueries;
                        PollTimer.AutoReset = true;
                        PollTimer.Start();
                    }
                }
            }
            public static void ScheduleQuery(GitHubContext context, Action<IRestResponse, GitHubContext> callback) 
            { 
                lock (mut) 
                {
                    // basically shit-hashing based on relevant data
                    // don't change without also fixing access token assumption in execute queries
                    string mapKey = context.ProjectEndpoint + context.AccessToken;
                    List < (GitHubContext, Action<IRestResponse, GitHubContext>)> current;
                    if (!GithubQueue.TryGetValue(mapKey, out current))
                    {
                        var newList = new List<(GitHubContext, Action<IRestResponse, GitHubContext>)>();
                        newList.Add((context, callback));
                        GithubQueue[mapKey] = newList;
                    }
                    else
                    {
                        current.Add((context, callback));
                    }
                }
            }

            private static void ExecuteQueries(object sender, ElapsedEventArgs e)
            {
                Action<IRestResponse> callback = r =>
                {
                    if (r.Request is BatchedRequest)
                    {
                        BatchedRequest owningRequest = (BatchedRequest)r.Request;
                        var listeners = owningRequest.Listeners;

                        foreach(var listener in listeners)
                        {
                            listener.Item2.Invoke(r, listener.Item1);
                        }
                    }
                };

                // forgive me father
                IEnumerable<KeyValuePair<string, List<(GitHubContext, Action<IRestResponse, GitHubContext>)>>> queriesCopy;
                // don't want to hold this mutex for too long, so make a copy of the queue
                lock (mut)
                {
                    queriesCopy = GithubQueue.ToArray();
                    GithubQueue.Clear();
                }

                foreach (var query in queriesCopy)
                {
                    // they all have the same access token, guaranteed by hash key upon insertion
                    GitHubContext gitHubContext = query.Value[0].Item1;


                    RestClient client = new RestClient(gitHubContext.ProjectEndpoint);
                    var authen = new JwtAuthenticator(gitHubContext.AccessToken);
                    client.Authenticator = authen;

                    BatchedRequest request = new BatchedRequest("events");

                    // eventually set this up and only dispatch a real poll if the info has changed
                    //if (gitHubContext.Etag != null)
                    //{
                    //    request.AddHeader("If-None-Match", gitHubContext.Etag);
                    //}

                    request.Listeners = query.Value;

                    client.ExecuteAsync(request, callback);
                }
            }
        }
    }
}
