using System;
using System.Collections.Generic;
using System.Data;
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
using static System.Net.WebRequestMethods;

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

        public static DataContractJsonSerializerSettings SerializerSettings { get; private set; }

        #endregion


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

        bool ShouldReportEvent(JsonDocument doc, string eventHeaderType, string eventMessage)
        {
            try
            {
                string repoURL = doc.RootElement.GetProperty("repository").GetProperty("full_name").ToString();
                foreach (GitHubContext gitHubContext in ActiveChannelContexts)
                {
                    if (gitHubContext.ProjectEndpoint.ToLower() == repoURL.ToLower())
                    {
                        return DoesEventPassFilter(doc, eventHeaderType, eventMessage);
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

        bool DoesEventPassFilter(JsonDocument doc, string eventHeaderType, string eventMessage)
        {
            if(eventMessage.Length == 0)
            {
                return false;
            }

            // basically ignore most check messages, unless there's a failure
            if (eventHeaderType == "check_run")
            {
                return false;
            }

            // delete messages are handled by push
            if (eventHeaderType == "delete")
            {
                return false;
            }

            if (eventHeaderType == "check_suite")
            {
                return false;

                // lol maybe we'll use this someday
                //JsonElement root = doc.RootElement;
                //string status = root.GetProperty("status").GetString();
                //if (status != "completed")
                //{
                //    return false;
                //}

                //// conclusion only valid if status is completed
                //string conclusion = root.GetProperty("conclusion").GetString();
                //if(conclusion == "success" || conclusion == "neutral")
                //{
                //    return false; 
                //}
            }

            return true;
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
            string formattedMessage = "";
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
                    case "check_suite":
                        {
                            //var appNode = root.GetProperty("app");
                            //string id = root.GetProperty("id").GetString();
                            //string status = root.GetProperty("status").GetString();
                            //bool bCompleted = status == "completed";
                            //string conclusion = bCompleted ? root.GetProperty("conclusion").GetString() : "";
                            //string url = root.GetProperty("check_runs_url").GetString();
                            //string appUrl = appNode.GetProperty("html_url").GetString();
                            //string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            //formattedMessage = $"Check Suite on  {repo} {status}. {conclusion} - {url}";
                        }
                        break;
                    case "create":
                        {
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string refName = root.GetProperty("ref").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string type = root.GetProperty("ref_type").GetString();
                            string description = root.GetProperty("description").GetString();
                            string pusherType = root.GetProperty("pusher_type").GetString();
                            formattedMessage = $"{pusherType} {user} created {type} \"{refName}\" in {repo} - {description}";
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
                            formattedMessage = $"{user} {action} discussion \"{discussionTitle}\" in {repo} - {discussionURL}";
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
                            formattedMessage = $"{user} {action} discussion comment in \"{discussionTitle}\" on {repo} - {commentUrl}";
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
                            string stargazerCount = root.GetProperty("repository").GetProperty("stargazers_count").GetString();
                            formattedMessage = $"{user} {action} star on {repo}. {stargazerCount} stargazers. - {url}";
                        }
                        break;
                    case "fork":
                        {
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string newUrl = root.GetProperty("html_url").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            formattedMessage = $"{user} forked {repo} - {newUrl}";
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
                            formattedMessage = $"{user} {action} page \"{pageName}\" in {repo}: {pageTitle} - {pageUrl}";
                        }
                        break;
                    case "issue_comment":
                        {
                            var comment = root.GetProperty("comment");
                            var issue = root.GetProperty("issue");
                            string action = root.GetProperty("action").GetString();
                            string commitId = comment.GetProperty("commit_id").GetString();
                            string commentURL = comment.GetProperty("html_url").GetString();
                            // prune away most of the commit hash
                            commentURL = commentURL.Replace(commitId, commitId.Substring(0, 8));
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string issueTitle = issue.GetProperty("title").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            formattedMessage = $"{user} {action} comment on {repo}:\"{issueTitle}\" - {commentURL}";
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
                            formattedMessage = $"{user} {action} issue in {repo}: \"{issueTitle}\" - {issueURL}";
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
                            formattedMessage = $"{user} {action} pull request in {repo}:\"{requestTitle}\". {requestURL}";
                        }
                        break;
                    case "push":
                        {
                            var commits = root.GetProperty("commits");
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string compareURL = root.GetProperty("compare").GetString();
                            JsonElement[] commitIterator = commits.EnumerateArray().ToArray();
                            int commitCount = commitIterator.Length;
                            {
                                StringBuilder messageAccumulator = new StringBuilder();
                                if (commitCount == 0)
                                {
                                    bool bCreated = root.GetProperty("created").GetBoolean();
                                    bool bDeleted = root.GetProperty("deleted").GetBoolean();
                                    string refType = root.GetProperty("ref").GetString();
                                    string verb = "";
                                    if (bCreated)
                                    {
                                        verb += "created";
                                    }
                                    if (bDeleted)
                                    {
                                        verb += "deleted";
                                    }
                                    messageAccumulator.Append($"{user} pushed: {verb} {refType} to {repo}. - {compareURL}");
                                }
                                else if (commitCount == 1)
                                {
                                    string hash = commitIterator[0].GetProperty("id").ToString();
                                    string commitURL = $"https://www.github.com/{repo}/commit/{hash.Substring(0, 8)}";
                                    messageAccumulator.Append($"{user} pushed a commit to {repo}. {commitURL} -");
                                }
                                else
                                {
                                    messageAccumulator.Append($"{user} pushed {commitCount} commits to {repo}. {compareURL} -");
                                    for (int i = 0; i < commitCount; ++i)
                                    {
                                        string message = commitIterator[i].GetProperty("message").ToString();
                                        messageAccumulator.Append($"\"{message}\"");
                                        if (i != commitCount - 1)
                                        {
                                            messageAccumulator.Append(" || ");
                                        }
                                    }
                                }

                                // truncate to fit into message
                                const int LengthTarget = 480;
                                if(messageAccumulator.Length > LengthTarget)
                                {
                                    messageAccumulator.Remove(LengthTarget - 1, messageAccumulator.Length - LengthTarget);
                                    messageAccumulator.Append("...");
                                }
                                formattedMessage = messageAccumulator.ToString();
                            }

                        }
                        break;
                    case "release":
                        {
                            var release = root.GetProperty("release");
                            string tagName = release.GetProperty("tag_name").GetString();
                            string releaseName = release.GetProperty("name").GetString();
                            bool bIsDraft = release.GetProperty("draft").GetBoolean();
                            bool bIsPrerelease = release.GetProperty("prerelease").GetBoolean();
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string action = root.GetProperty("action").GetString();
                            formattedMessage = $"{user} {action} release {(bIsDraft ? "[Draft]" : "")} {(bIsPrerelease ? "[Prerelease]" : "")} {tagName}-{releaseName} on {repo}";
                        }
                        break;
                    case "watch":
                        {
                            string user = root.GetProperty("sender").GetProperty("login").GetString();
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            string action = root.GetProperty("action").GetString();
                            string watchersCount = root.GetProperty("repository").GetProperty("watchers_count").GetString();
                            string url = root.GetProperty("repository").GetProperty("html_url").GetString();

                            formattedMessage = $"{user} {action} watch on {repo}. {watchersCount} watchers. - {url}";
                        }
                        break;
                    case "delete":
                        {
                            string repo = root.GetProperty("repository").GetProperty("full_name").GetString();
                            formattedMessage = "Delete in {repo}";
                        }
                        break;
                    default:
                        {
                            //formattedMessage = $"Unhandled event type: {eventType} by {commonFields.ActorName}";
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
                        //Console.WriteLine($"Received github event: {eventHeaderType}");

                        if(eventHeaderType == "ping")
                        {
                            return true;
                        }    

                        using (JsonDocument doc = JsonDocument.Parse(payload))
                        {
                            string eventMessage = GitHubModule.FormatEventString(doc, eventHeaderType);

                            Fatty.PrintToScreen(eventMessage, ConsoleColor.White);

                            lock(RegistrationMutex)
                            {
                                foreach(GitHubModule mod in ListenerModules)
                                {
                                    if(mod.ShouldReportEvent(doc, eventHeaderType, eventMessage))
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
