using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Threading;

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

        public static DataContractJsonSerializerSettings SerializerSettings { get; private set; }


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

        class BatchedRequest : HttpRequestMessage
        {
            public BatchedRequest(string resource) : base(HttpMethod.Get, resource) { }

            public List<(GitHubContext, Action<HttpWebResponse, GitHubContext>)> Listeners { get; set; }
        }

        class FattyRequest : HttpRequestMessage
        {
            public FattyRequest(string resource) : base(HttpMethod.Get, resource) { }
            public object UserState { get; set; }
        }


        private List<GitHubContext> ActiveChannelContexts;

        public GitHubModule()
        {
            ActiveChannelContexts = new List<GitHubContext>();

            //github uses iso 8601 which throws the default serializer settings for a loop
            if (SerializerSettings == null)
            {
                SerializerSettings = new DataContractJsonSerializerSettings();
                SerializerSettings.DateTimeFormat = new DateTimeFormat("yyyy-MM-ddTHH:mm:ssZ");
            }

            GitHubHttpListener.Init(this);
        }

        ~GitHubModule() 
        {
            GitHubHttpListener.RemoveListener(this);
        }


        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("ghlimit", GitHubLimitCommand, "Checks API endpoint Limits"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("ghlimit");
        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);


            GitHubContextListing contextListing = FattyHelpers.DeserializeFromPath<GitHubContextListing>("GitHub.cfg");

            // only care about channels that this channel is looking at
            foreach (GitHubContext ghContext in contextListing.AllContexts)
            {
                if (ghContext.ServerName == OwningChannel.ServerName && ghContext.ChannelName == OwningChannel.ChannelName)
                {
                    ActiveChannelContexts.Add(ghContext);
                }
            }
        }

        void ReportEvent(JsonDocument doc, string eventText)
        {
            OwningChannel.SendChannelMessage(eventText);
        }

        bool ShouldReportEvent(JsonDocument doc)
        {
            try
            {
                string repoURL = doc.RootElement.GetProperty("repository").GetProperty("full_name").ToString();
                foreach (GitHubContext gitHubContext in ActiveChannelContexts)
                {
                    if (gitHubContext.ProjectEndpoint.ToLower() == repoURL.ToLower())
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Fatty.PrintWarningToScreen(ex);
                return false;
            }
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

        private class CommonFields
        {
            public string RepoName;
            public string ActorName;
            public CommonFields(JsonElement root)
            {
                try
                { RepoName = root.GetProperty("repository").GetProperty("full_name").GetString(); }
                catch { }
                try
                { ActorName = root.GetProperty("sender").GetProperty("login").GetString(); }
                catch { }

            }
        }

        public static string FormatEventString(JsonDocument input, string eventType)
        {
            string formattedMessage = "dunno, lol";
            try
            {
                JsonElement root = input.RootElement;
                CommonFields commonFields = new CommonFields(root);

                
                switch (eventType)
                {
                    case "commit_comment":
                        {
                            var comment = root.GetProperty("comment");
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string action = root.GetProperty("action").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string url = comment.GetProperty("html_url").GetString();
                            string body = comment.GetProperty("body").GetString();
                            const int previewLength = 26;
                            string bodySnippet = body.Substring(0, Math.Min(body.Length, previewLength));
                            if (body.Length > previewLength)
                                bodySnippet += "...";
                            formattedMessage = $"{commonFields.ActorName} {action} a commit comment in {repo} - {url} //{bodySnippet}";
                        }
                        break;
                    case "create":
                        {
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string type = root.GetProperty("ref_type").GetString();
                            string description = root.GetProperty("description").GetString();
                            formattedMessage = $"{user} created {type} in {repo} - {description}";
                        }
                        break;
                    case "discussion":
                        {
                            var discussion = root.GetProperty("discussion");
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string discussionURL = discussion.GetProperty("html_url").GetString();
                            string discussionTitle = discussion.GetProperty("title").GetString();
                            string action = root.GetProperty("action").GetString();
                            formattedMessage = $"{user} {action} discussion \"{repo}/{discussionTitle}\" - {discussionURL}";
                        }
                        break;
                    case "discussion_comment":
                        {
                            var discussion = root.GetProperty("discussion");
                            var comment = root.GetProperty("comment");
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string commentUrl = comment.GetProperty("html_url").GetString();
                            string discussionTitle = discussion.GetProperty("title").GetString();
                            string action = root.GetProperty("action").GetString();
                            formattedMessage = $"{user} {action} discussion comment on \"{repo}/{discussionTitle}\" - {commentUrl}";
                        }
                        break;
                    case "repository":
                        {
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string action = root.GetProperty("action").GetString();
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string url =  root.GetProperty("repository").GetProperty("html_url").GetString();
                            formattedMessage = $"{user} {action} {repo} - {url}";
                        }
                        break;
                    case "star":
                        {
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string action = root.GetProperty("action").GetString();
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string url = root.GetProperty("repository").GetProperty("html_url").GetString();
                            formattedMessage = $"{user} {action} star for {repo} - {url}";
                        }
                        break;
                    case "fork":
                        {
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string url = root.GetProperty("repository").GetProperty("html_url").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            formattedMessage = $"{user} forked {repo} - {url}";
                        }
                        break;
                    case "gollum":
                        {
                            var pages = root.GetProperty("pages");
                            string action = pages.GetProperty("action").GetString();
                            string pageTitle = pages.GetProperty("title").GetString();
                            string pageName = pages.GetProperty("page_name").GetString();
                            string pageUrl = pages.GetProperty("html_url").GetString();
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repoUrl = root.GetProperty("repository").GetProperty("html_url").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            formattedMessage = $"{user} {action} page.  {repo}/{pageTitle} \"{pageName}\" - {pageUrl}";
                        }
                        break;
                    case "issue_comment":
                        {
                            var comment = root.GetProperty("comment");
                            var issue = root.GetProperty("issue");
                            string action = root.GetProperty("action").GetString();
                            string commentURL = comment.GetProperty("html_url").GetString();
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string issueTitle = issue.GetProperty("title").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            formattedMessage = $"{user} {action} comment on {repo}/{issueTitle}. {commentURL}";
                        }
                        break;
                    case "issues":
                        {
                            var issue = root.GetProperty("issue");
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string action = root.GetProperty("action").GetString();
                            string issueTitle = issue.GetProperty("title").GetString();
                            string issueURL = issue.GetProperty("html_url").GetString();
                            formattedMessage = $"{user} {action} issue \"{repo}/{issueTitle}\". {issueURL}";
                        }
                        break;
                    case "pull_request":
                        {
                            var pullRequest = root.GetProperty("pull_request");
                            string requestTitle = pullRequest.GetProperty("title").GetString();
                            string requestURL = pullRequest.GetProperty("html_url").GetString();
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string action = root.GetProperty("action").GetString();
                            formattedMessage = $"{user} {action} pull request. \"{repo}/{requestTitle}\". {requestURL}";
                        }
                        break;
                    case "push":
                        {
                            var commits = root.GetProperty("commits");
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            JsonElement[] commitIterator = commits.EnumerateArray().ToArray();
                            int commitCount = commitIterator.Length;
                            {
                                StringBuilder messageAccumulator = new StringBuilder();

                                messageAccumulator.Append($"{user} pushed {commitCount} commits to {repo}: ");
                                for (int i = 0; i < commitCount; ++i)
                                {
                                    string hash = commitIterator[i].GetProperty("id").ToString();
                                    string message = commitIterator[i].GetProperty("message").ToString();
                                    string commitURL = $"https://www.github.com/{repo}/commit/{hash.Substring(0, 8)}";
                                    messageAccumulator.Append($"\"{message}\" - {commitURL}");
                                    if (i != commitCount - 1)
                                    {
                                        messageAccumulator.Append(" || ");
                                    }
                                }

                                formattedMessage = messageAccumulator.ToString();
                            }

                        }
                        break;
                    default:
                        {
                            formattedMessage = $"Unhandled event type: {eventType} by {commonFields.ActorName}";
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Fatty.PrintWarningToScreen(ex);
                formattedMessage = $"exception while handling {eventType}. Check logs";
            }

            return formattedMessage;
        }

        string FormatRestEventString(GitHubEvent evnt, GitHubContext context)
        {
            switch (evnt.EventType)
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

        static string FormatPushEventString(GitHubEvent evnt)
        {
            int commitCount = evnt.Payload.PayloadSize;
            StringBuilder messageAccumulator = new StringBuilder();

            messageAccumulator.Append($"{evnt.Actor.DisplayName} pushed {evnt.Payload.PayloadSize} commits to {evnt.Repo.RepoName}: ");
            for (int i = 0; i < commitCount; ++i)
            {
                string commitURL = $"https://www.github.com/{evnt.Repo.RepoName}/commit/{evnt.Payload.Commits[i].Hash.Substring(0, 8)}";
                messageAccumulator.Append($"\"{evnt.Payload.Commits[i].Message}\" - {commitURL}");
                if (i != commitCount - 1)
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

        public void GitHubLimitCommand(string ircUser, string ircChannel, string message)
        {
            GitHubContext firstContext = ActiveChannelContexts[0];
            if (firstContext != null)
            {
                HttpClient client = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.github.com")
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstContext.AccessToken);
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FattyBot", "0.3"));

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "rate_limit");

                var response = client.Send(request);
                if (response.IsSuccessStatusCode)
                {
                    List<string> LimitStrings = new List<string>();
                    foreach (var item in response.Headers)
                    {
                        if (item.Key.Contains("limit", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                string itemName = item.Key;
                                string itemValue = item.Value.First();
                                if (item.Key == "X-RateLimit-Reset")
                                {
                                    long unixTime = Convert.ToInt64(itemValue);
                                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTime);
                                    DateTime dateTime = dateTimeOffset.LocalDateTime;
                                    itemValue = dateTime.ToString();
                                }
                                string limitEntry = $"{itemName}={itemValue}";
                                LimitStrings.Add(limitEntry);
                            }
                            catch (Exception ex)
                            {
                                Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                            }
                        }
                    }
                    if (LimitStrings.Count > 0)
                    {
                        string limits = string.Join(" - ", LimitStrings);
                        limits = limits.TrimEnd(new char[] { ' ', '-' });
                        OwningChannel.SendChannelMessage(limits);
                    }
                    else
                    {
                        OwningChannel.SendChannelMessage("No limit headers found");
                    }
                }
                else
                {
                    OwningChannel.SendChannelMessage("Failed, sry");
                }

            }
        }

        static public class GitHubHttpListener
        {
            private static Mutex initializerMutex = new Mutex();
            private static Mutex RegistrationMutex = new Mutex();
            private static Thread GithubListenerThread;
            private static List<GitHubModule> ListenerModules = new List<GitHubModule>(); 

            public static void Init(GitHubModule ListenerModule)
            {
                lock (initializerMutex)
                {
                    if (GithubListenerThread == null)
                    {
                        GithubListenerThread = new Thread(new ThreadStart(Listen));
                        GithubListenerThread.Name = "GithubListner";
                        GithubListenerThread.Start();
                    }
                }
                lock(RegistrationMutex)
                {
                    ListenerModules.Add(ListenerModule);
                }
            }
            public static void Listen()
            {
                while (true)
                {
                    try
                    {
                        string testURL = "http://*:4950/";
                        HttpListener listener = new HttpListener();
                        listener.Prefixes.Add(testURL);
                        listener.Start();
                        Fatty.PrintToScreen($"Listening for GitHub webhooks on \"{testURL}\"", ConsoleColor.Cyan);

                        while (true)
                        {
                            HttpListenerContext context = listener.GetContext();
                            HttpListenerRequest request = context.Request;

                            if (request.HttpMethod == "POST")
                            {
                                HandlePost(request);

                                context.Response.StatusCode = 200;
                                context.Response.Close();
                            }
                            else
                            {
                                // Respond with a 405 Method Not Allowed if the request is not a POST
                                context.Response.StatusCode = 405;
                                context.Response.Close();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Fatty.PrintWarningToScreen(ex);
                    }

                    // rest 10 seconds before trying to restart listener thread
                    Thread.Sleep(10000);
                }
            }

            public static bool HandlePost(HttpListenerRequest request)
            {
                try
                {
                    // Get the event type from the GitHub headers
                    string eventHeaderType = request.Headers["X-GitHub-Event"];

                    using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string payload = reader.ReadToEnd();
                        Console.WriteLine($"Received github event: {eventHeaderType}");
                        using (JsonDocument doc = JsonDocument.Parse(payload))
                        {
                            string eventMessage = GitHubModule.FormatEventString(doc, eventHeaderType);

                            Fatty.PrintToScreen(eventMessage, ConsoleColor.White);

                            lock(RegistrationMutex)
                            {
                                foreach(GitHubModule mod in ListenerModules)
                                {
                                    if(mod.ShouldReportEvent(doc))
                                    {
                                        mod.ReportEvent(doc, eventMessage);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fatty.PrintWarningToScreen(ex);
                    return false;
                }

                return true;
            }

            public static bool RemoveListener(GitHubModule module)
            {
                return ListenerModules.Remove(module);
            }
        }
    }
}
