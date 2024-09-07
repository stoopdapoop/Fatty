using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Fatty
{
    class SteamModule : FattyModule
    {
        // fields populated by data contract.
#pragma warning disable 0649
        [DataContract]
        public class SteamConfig 
        {
            [DataMember(IsRequired = true)]
            public string APIKey;

            [DataMember(IsRequired = true)]
            public int PollFrequencyMinutes;

            [DataMember]
            public List<SteamChannelContext> Contexts;
        }

        [DataContract]
        public class SteamChannelContext
        {
            [DataMember(IsRequired = true)]
            public string ServerName;

            [DataMember(IsRequired = true)]
            public string ChannelName;

            [DataMember(IsRequired = true)]
            public int MinPlayerThreshold;

            [DataMember(IsRequired = true)]
            public int MessageCooldownMinutes;

            [DataMember]
            public bool ShouldPoll;
        }

        [DataContract]
        public class SteamServerResult
        {
            [DataMember(Name = "response")]
            public SteamServerResponse Response;
        }

        [DataContract]
        public class SteamServerResponse
        {
            [DataMember(Name = "servers")]
            public List<SteamGameServer> Servers;
        }

        [DataContract]
        public class SteamGameServer
        {
            [DataMember(Name = "addr")]
            public string IPAddress;

            [DataMember(Name = "steamid")]
            public string SteamID;

            [DataMember(Name = "name")]
            public string ServerName;

            [DataMember(Name = "appid")]
            public long AppID;

            [DataMember(Name = "product")]
            public string ProductName;

            [DataMember(Name = "players")]
            public int PlayerCount;
            
        }

#pragma warning restore 0649

        SteamConfig Config;
                
        const string APIBaseAddress = "https://api.steampowered.com";
        const string ServerListEndpoint = "IGameServersService/GetServerList/v1/";
        const string FilterParams = @"gamedir\NeotokyoSource\empty\1";
        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
        }

        public override void PostConnectionModuleInit()
        {
            base.PostConnectionModuleInit();

            // todo: make it so I only care about my own contexts
            Config = FattyHelpers.DeserializeFromPath<SteamConfig>("Steam.cfg");
            List<SteamChannelContext> activeContexts = new List<SteamChannelContext>();

            foreach(var context in Config.Contexts)
            {
                if (context.ServerName.Equals(OwningChannel.ServerName, StringComparison.OrdinalIgnoreCase))
                {
                    if (context.ChannelName.Equals(OwningChannel.ChannelName, StringComparison.OrdinalIgnoreCase))
                    {
                        activeContexts.Add(context);
                    }
                }
            }

            Config.Contexts = activeContexts;
        }

        public override void RegisterEvents()
        {
            OwningChannel.ChannelJoinedEvent += OnChannelJoined;
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("neotokyo");
            CommandNames.Add("nt");
        }

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("neotokyo", NeotokyoCommand, "checks to see if there are any populated Neotokyo servers"));
            Commands.Add(new UserCommand("nt", NeotokyoCommand, "checks to see if there are any populated Neotokyo servers"));
        }

        async void OnChannelJoined(string ircChannel)
        {
            // todo: move this somewhere else
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            Random rand = new Random();
            
            foreach (SteamChannelContext context in Config.Contexts)
            {
                if (context.ShouldPoll)
                {
                    // make each instance of the module wait some amount of time so we don't get in trouble for spamming.
                    await Task.Delay(rand.Next(500, 1000));
                    PollNeotokyoServers(context, tokenSource.Token);
                }
            }
        }

        private void NeotokyoCommand(string ircUser, string ircChannel, string message)
        {
            try
            {
                HttpClient client = new HttpClient()
                {
                    BaseAddress = new Uri(APIBaseAddress)
                };

                HttpRequestMessage request = GetNeotokyoServerRequest();

                var result = client.Send(request);

                if (result.IsSuccessStatusCode)
                {
                    SteamServerResult pollResult = FattyHelpers.DeserializeFromJsonString<SteamServerResult>(result.Content.ToString());
                    SteamServerResponse pollResponse = pollResult.Response;
                    bool reported = false;

                    if (pollResponse != null)
                    {
                        if (pollResponse.Servers != null && pollResponse.Servers.Count > 0)
                        {
                            int maxPop = 0;
                            string MaxServerName = "";
                            foreach (var server in pollResponse.Servers)
                            {
                                if(maxPop < server.PlayerCount)
                                {
                                    maxPop = server.PlayerCount;
                                    MaxServerName = server.ServerName;
                                }
                                maxPop = Math.Max(maxPop, server.PlayerCount);
                            }

                            reported = true;
                            OwningChannel.SendChannelMessage($"there are {pollResponse.Servers.Count} populated servers. The highest population is {maxPop} : {MaxServerName}");         
                        }
                    }
                    if(!reported)
                    {
                        OwningChannel.SendChannelMessage("no populated servers :[");
                    }
                }
                else
                {
                    OwningChannel.SendChannelMessage("something bad happened");
                }    
            }

            catch (Exception ex)
            {
                OwningChannel.SendChannelMessage("Something messed up");
                Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
            }

        }

        async void PollNeotokyoServers(SteamChannelContext context, CancellationToken cancel)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(APIBaseAddress)
            };

            HttpRequestMessage request = GetNeotokyoServerRequest();

            TimeSpan PollInterval = TimeSpan.FromMinutes(Config.PollFrequencyMinutes);
            TimeSpan SleepTime = TimeSpan.FromMinutes(context.MessageCooldownMinutes);

            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var result = await client.SendAsync(request);
                    if (result.IsSuccessStatusCode)
                    {
                        List<SteamGameServer> PopulatedServers = new List<SteamGameServer>();
                        try
                        {
                            // todo: read content properly instead of doing this weird string transcode
                            SteamServerResult pollResult = FattyHelpers.DeserializeFromJsonString<SteamServerResult>(result.Content.ToString());
                            SteamServerResponse pollResponse = pollResult.Response;
                            if (pollResponse != null)
                            {
                                if (pollResponse.Servers != null && pollResponse.Servers.Count > 0)
                                {
                                    foreach (var server in pollResponse.Servers)
                                    {
                                        if (server.PlayerCount >= context.MinPlayerThreshold)
                                        {
                                            PopulatedServers.Add(server);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                        }

                        if(PopulatedServers.Count > 0)
                        {
                            if (PopulatedServers.Count == 1)
                            {
                                OwningChannel.SendChannelMessage($"Neotokyo server \"{PopulatedServers[0].ServerName}\" has { PopulatedServers[0].PlayerCount } nerds in it");
                            }
                            else
                            {
                                OwningChannel.SendChannelMessage($"{PopulatedServers.Count} Neotokyo servers have {context.MinPlayerThreshold} or more nerds in them");
                            }

                            await Task.Delay(SleepTime);
                        }
                        else
                        {
                            await Task.Delay(PollInterval);
                        }
                    }
                    else
                    {
                        await Task.Delay(PollInterval);
                    }
                }
                catch(Exception ex)
                {
                    Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                }
            }
        }

        private HttpRequestMessage GetNeotokyoServerRequest()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ServerListEndpoint);
            //request.AddQueryParameter("key", Config.APIKey);
            request.Properties["key"] = Config.APIKey;
            //request.AddQueryParameter("filter", FilterParams);
            request.Properties["filter"] = FilterParams;

            return request;
        }
    }
}
