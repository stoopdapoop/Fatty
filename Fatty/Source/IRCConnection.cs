using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace Fatty
{
    public partial class IRCConnection
    {
        public ServerContext Context { get; set; }

        private TcpClient IrcConnection { get; set; }
        private NetworkStream IrcStream { get; set; }
        private StreamWriter IrcWriter { get; set; }
        private StreamReader IrcReader { get; set; }

        private WelcomeProgress IRCWelcomeProgress;

        private Object WriteLock = new object();

        private Thread ListenerThread;

        private Thread HealthThread;

        private bool ListenerIsListening = false;

        private bool Quitting = false;

        private bool WaitingOnNames = false;
        private Object NamesLock = new object();
        private List<NamePromise> NamePromises = new List<NamePromise>();
        private EventWaitHandle NamesWaitHandle;

        public event PrivateMessageDelegate PrivateMessageEvent;
        public event NoticeWhisperDelegate NoticeEvent;

        private class NamePromise
        {
            public NamePromise(ChannelContext inContext) { requestingContext = inContext; }
            //public string[] names;
            public ChannelContext requestingContext;
            public bool finished = false;
        }

        public IRCConnection(ServerContext context)
        {
            this.Context = context;
        }

#nullable enable
        private void SendUserInfo(string nick, string user, string? pass)
        {
            // order defined by rfc
            if (pass != null)
            {
                // this is kind of silly, but the server password twitch is derived from the auth token, and the auth token changes frequently.
                if (pass.Equals("TwitchMagicString"))
                {
                    SendServerMessage($"PASS {TwitchModule.GetTwitchServerPassword()}");
                }
                else
                {
                    SendServerMessage($"PASS {pass}");
                }
            }
#nullable disable
            if (Context.ServerCaps != null)
            {
                SendServerMessage("CAP LS 302");
            }
            SendServerMessage($"NICK {nick}");
            SendServerMessage($"USER {nick} 0 * :{user}");
            if (Context.ServerCaps != null)
            {
                foreach (string reqCap in Context.ServerCaps)
                {
                    SendCapRequest(reqCap);
                }
                SendServerMessage("CAP END");
            }

        }

        public void ConnectToServer()
        {
            Fatty.PrintToScreen("Attempting to connect to: {0}:{1}", Context.ServerURL, Context.ServerPort);

            if(HealthThread == null)
            {
                HealthThread = new Thread(new ThreadStart(ListenForDisconnect));
                HealthThread.Name = "HealthThread";
                HealthThread.Start();
            }

            RegisterEventCallbacks();

            try
            {
                //Establish connection
                this.IrcConnection = new TcpClient(Context.ServerURL, Context.ServerPort);
                // wait 20 minutes for timeout
                this.IrcConnection.ReceiveTimeout = 1000 * 60 * 20;
                this.IrcStream = this.IrcConnection.GetStream();
                if (Context.UseSSL)
                {
                    SslStream sslStream = new SslStream(IrcStream);
                    sslStream.AuthenticateAsClient(Context.ServerURL);
                    this.IrcReader = new StreamReader(sslStream);
                    this.IrcWriter = new StreamWriter(sslStream);
                }
                else
                {
                    this.IrcReader = new StreamReader(IrcStream);
                    this.IrcWriter = new StreamWriter(IrcStream);
                }

                IrcWriter.AutoFlush = true;
                IrcWriter.NewLine = "\r\n";


                Fatty.PrintToScreen("Connection Successful");

                // Spawn listener Thread
                ListenerThread = new Thread(new ThreadStart(ListenForServerMessages));
                ListenerThread.Name = "ListenerDispatchThread";
                ListenerThread.Start();

                // Send user info
                Fatty.PrintToScreen("Sending user info...");
                SendUserInfo(Context.Nick, Context.RealName, Context.ServerAuthPassword);

                foreach( ChannelContext chanContext in Context.Channels)
                {
                    chanContext.PostConnectionInitModules();
                }

            }
            catch (Exception e)
            {
                Fatty.PrintWarningToScreen($"Connection Failed: {e.Message}", e.StackTrace);
                if(ListenerThread != null && ListenerThread.IsAlive)
                {
                    Fatty.PrintWarningToScreen("Listener thread still running after connection failure!!!!");
                }
            }
        }


        public void SendMessage(string sendTo, string message)
        {
            string outputMessage = $"PRIVMSG {sendTo} :{message}";
            SendServerMessage(outputMessage);
        }

        public void SendNotice(string sendTo, string message, NoticeType type = NoticeType.NOTICE)
        {
            string outputMessage = $"{type.ToString()} {sendTo} :{message}";
            SendServerMessage(outputMessage);
        }

        public void SendCapRequest(string cap)
        {
            SendServerMessage($"CAP REQ :{cap}");
        }

        public string[] GetChannelUsers(ChannelContext ircChannel)
        {

            // this work gets picked up by the listener thread when the names results come back
            lock (NamesLock)
            {
                WaitingOnNames = true;
                Debug.Assert(NamesWaitHandle == null);

                NamesWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                NamePromises.Add(new NamePromise(ircChannel));
                SendServerMessage($"NAMES {ircChannel.ChannelName}");
            }

            if(NamesWaitHandle.WaitOne(1000))
            {

            }
            else
            {

            }
            // this is where I left off
            return Array.Empty<string>();
        }
        private void SendServerMessage(string format, params object[] args)
        {
            SendServerMessage(String.Format(format, args));
        }

        private void SendServerMessage(string message)
        {
            // remove newlines and carraige return
            string outMessage = String.Format("{0}", message.Replace("\n", " ").Replace("\r", " "));
            lock (WriteLock)
            {
                this.IrcWriter.WriteLine(outMessage);
            }

            if (message.StartsWith("PRIVMSG"))
            {
                Fatty.PrintToScreen(outMessage, ConsoleColor.Green);
            }
            else if (message.StartsWith("NOTICE"))
            {
                Fatty.PrintToScreen(outMessage, ConsoleColor.Cyan);
            }
            else if (!message.StartsWith("PONG"))
            {
                Fatty.PrintToScreen(outMessage, ConsoleColor.White);
            }
        }

        public void DisconnectOnExit()
        {
            if (!Quitting)
            {
                Fatty.PrintToScreen("Disconnecting Due to Exit");
                SendServerMessage(String.Format($"QUIT :{Context.QuitMessage}"));
            }
        }

        public bool IsConnectedToServer()
        {
            return ListenerIsListening;
        }

        private void ListenForServerMessages()
        {
            string ircResponse;
            ListenerIsListening = true;
            StreamReader reader = this.IrcReader;

            try
            {
                while ((ircResponse = reader.ReadLine()) != null)
                {
                        PrintServerMessage(ircResponse);
                        ThreadPool.QueueUserWorkItem(ThreadProc, ircResponse);
                }
            }
            catch (Exception e)
            {
                Fatty.PrintWarningToScreen($"Listener Exception: {e.Message}", e.StackTrace);
            }
            
            Fatty.PrintToScreen($"Listener Died ({Context.ServerName})", ConsoleColor.Yellow);
            ListenerIsListening = false;
        }

        // todo, use n'th index of space for substring instead of token join
        private void PrintServerMessage(string message)
        {
            try
            {
                ServerMessage serverMessage = new ServerMessage(message);

                if (serverMessage.Command == "PING")
                    return;

                if (serverMessage.Command == "PRIVMSG")
                {
                    string talkingUser = serverMessage.Prefix.Substring(0, serverMessage.Prefix.IndexOf('!')).TrimStart(':');
                    string userMessage = serverMessage.Params.Substring(serverMessage.Params.IndexOf(":") + 1);
                    string channelName = serverMessage.Params.Substring(0, serverMessage.Params.IndexOf(":") - 1);  

                    Fatty.PrintToScreen($"{channelName}<{talkingUser}>{userMessage}", ConsoleColor.DarkCyan);
                }
                else if (serverMessage.Command.Length == 3 && Char.IsDigit(serverMessage.Command[0]))
                {
                    string messagetext = serverMessage.Params.Substring(serverMessage.Params.IndexOf(":") + 1);
                    Fatty.PrintToScreen($"{Context.ServerName}:{serverMessage.Command}:{messagetext}", ConsoleColor.White);
                }
                else if (serverMessage.Command == "NOTICE" || serverMessage.Command == "WHISPER")
                {
                    string noticeSender = serverMessage.Prefix;
                    int maskDelimit = serverMessage.Prefix.IndexOf('!');
                    if (maskDelimit > -1) 
                    {
                        noticeSender = serverMessage.Prefix.Substring(0, maskDelimit);
                    }
                    string noticeMessage = serverMessage.Params.Substring(serverMessage.Params.IndexOf(":"));
                    Fatty.PrintToScreen($"{noticeSender}:{serverMessage.Command} {noticeMessage}", ConsoleColor.DarkRed);
                }
                else if (serverMessage.Command == "JOIN" || serverMessage.Command == "PART")
                {
                    bool bIsJoin = serverMessage.Command == "JOIN";
                    int startIndex = serverMessage.Prefix[0] == ':' ? 1 : 0;
                    int endIndex = serverMessage.Prefix.IndexOf('!');
                    string joiningUser = serverMessage.Prefix.Substring(startIndex, endIndex - startIndex);

                    Fatty.PrintToScreen($"{serverMessage.Params} <{joiningUser}> {serverMessage.Command}", bIsJoin ? ConsoleColor.Cyan : ConsoleColor.DarkYellow);
                }
                else
                {
                    Fatty.PrintToScreen(message);
                }
            }
            catch (Exception ex) 
            { 
                Fatty.PrintWarningToScreen($"{ex.Message} : While trying to display {message}", ex.StackTrace);
            }
        }

        void ThreadProc(object stateInfo)
        {
            // No state object was passed to QueueUserWorkItem, so stateInfo is null.
            DispatchMessageEvents((string)stateInfo);
        }
        
        private void RuntimeJoinChannel(string channelName)
        {
            ChannelContext newContext = new ChannelContext();
            newContext.ChannelName = channelName;
            newContext.Initialize(Context);
            Context.Channels.Add(newContext);
            JoinChannel(channelName);
        }

        private void JoinChannel(string channelName)
        {
            SendServerMessage($"JOIN {channelName}");
        }

        private void PartChannel(string channelName)
        {
            SendServerMessage($"PART {channelName} :{Context.QuitMessage}");
        }

        public class ServerMessage
        {
            public Dictionary<string, string> Tags;
            public string Prefix;
            public string Command;
            public string Params;
            public string CompleteMessage;

            public ServerMessage(string rawMessage)
            {
                // <message> ::= ['@' <tags> <SPACE>] [':' <prefix> <SPACE> ] <command> [params] <crlf>
                CompleteMessage = rawMessage;

                try
                {
                    int nextStart = 0;
                    bool HasTags = rawMessage[0] == '@';
                    if (HasTags)
                    {
                        Tags = new Dictionary<string, string>();
                        int tagEnd = rawMessage.IndexOf(' ');

                        string tags = rawMessage.Substring(1, tagEnd - 1);
                        string[] tagPairs = tags.Split(';');
                        foreach (string tagPair in tagPairs)
                        {
                            string[] TagElements = tagPair.Split('=');
                            Tags[TagElements[0]] = TagElements[1];
                        }
                        nextStart = tagEnd + 1;
                    }

                    
                    string nextSection = rawMessage.Substring(nextStart);
                    if (nextSection[0] == ':')
                    {
                        nextStart = nextSection.IndexOf(' ') + 1;
                        Prefix = nextSection.Substring(1, nextStart - 2);

                        nextSection = nextSection.Substring(nextStart);
                    }

                    char[] escapes = { ' ', '\r', '\n' };
                    nextStart = nextSection.IndexOfAny(escapes);

                    Command = nextSection.Substring(0, nextStart);

                    Params = nextSection.Substring(nextStart + 1);
                }
                catch (Exception ex)
                {
                    Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                }
            }
        }

        private void DispatchMessageEvents(string response)
        {
            ServerMessage responseMessage = new ServerMessage(response);

            switch (responseMessage.Command)
            {
                //// welcome messages
                case "001":
                case "002":
                case "003":
                case "004":
                    HandleWelcomeMessage(responseMessage);
                    break;
                case "PING":
                    HandlePing(responseMessage);
                    break;
                case "PRIVMSG":
                    HandlePrivMsg(responseMessage);
                    break;
                case "NOTICE":
                case "WHISPER":
                    HandleWhisperNotice(responseMessage);
                    break;
                case "INVITE":
                    HandleInvite(responseMessage);
                    break;
                case "GLOBALUSERSTATE":
                    HandleUserState(responseMessage, UserStateType.Global);
                    break;
                case "USERSTATE":
                    HandleUserState(responseMessage, UserStateType.User);
                    break;
                case "ROOMSTATE":
                    HandleUserState(responseMessage, UserStateType.Room);
                    break;
                case "USERNOTICE":
                    HandleUserNotice(responseMessage);
                    break;
                //// RPL_NAMREPLY
                //case "353":
                //    {
                //        HandleNameList(commandTokens);
                //        break;
                //    }
                case "432":
                    SendMessage("nickserv", $"IDENTIFY {Context.NickAuthPassword}");
                    break;
                case "JOIN":
                case "PART":
                    {
                        HandleUserJoinPart(responseMessage);
                        break;
                    }
            }
        }

        private void HandleWelcomeMessage(ServerMessage message)
        {
            int welcomeID =  int.Parse(message.Command);
            IRCWelcomeProgress.NotifyOfMessage(welcomeID);
        }

        private void HandlePrivMsg(ServerMessage message)
        {
            string userSender = message.Prefix.Substring(0, message.Prefix.IndexOf('!'));
            int DelimPos = message.Params.IndexOf(':');
            string messageTo = message.Params.Substring(0, DelimPos - 1);
            string chatMessage = message.Params.Substring(DelimPos + 1);
            
            if (messageTo[0] == '#' || messageTo[0] == '&')
            {
                Context.HandleServerMessage(message.Tags, userSender, messageTo, chatMessage);
            }
            else if (chatMessage.StartsWith('\u0001') && chatMessage.EndsWith('\u0001'))
            {
                string trimmedMessage = chatMessage.Trim('\u0001');
                string[] trimmedChunks = trimmedMessage.Split();
                switch (trimmedChunks[0])
                {
                    case "PING":
                        SendNotice(userSender, String.Format("\u0001PING {0} PONG\u0001", trimmedChunks[1]));
                        break;
                    case "VERSION":
                        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                        string version = fvi.FileVersion;
                        SendNotice(userSender, String.Format("\u0001VERSION Fatty v{0}\u0001", version));
                        break;
                    case "TIME":
                        SendNotice(userSender, String.Format("\u0001TIME {0}\u0001", DateTime.Now.ToString()));
                        break;
                    case "FINGER":
                        SendNotice(userSender, "\u0001FINGER No!\u0001");
                        break;
                }
            }
            else
            {
                if (PrivateMessageEvent != null)
                {
                    foreach (PrivateMessageDelegate privDel in PrivateMessageEvent.GetInvocationList())
                    {
                        privDel(userSender, chatMessage);
                    }
                }
            }
        }

        private void HandleWhisperNotice(ServerMessage serverMessage)
        {
            bool bAdminCommand = false;
            // if notice is from a user and not the server
            int maskDelimit = serverMessage.Prefix.IndexOf('!');
            if (maskDelimit > -1)
            {
                string userSender = serverMessage.Prefix.Substring(0, maskDelimit);
                try
                {
                    if (Context.IsAuthenticatedUser(serverMessage.Prefix))
                    {
                        // first word is command, the rest is argument.
                        int DelimPos = serverMessage.Params.IndexOf(':');
                        string message = serverMessage.Params.Substring(DelimPos + 1);
                        int commandDelimitPos = message.IndexOf(' ');
                        string command = message.Substring(0, commandDelimitPos).ToLower();
                        string args = message.Substring(commandDelimitPos + 1);

                        switch (command)
                        {
                            case "join":
                                RuntimeJoinChannel(args);
                                bAdminCommand = true;
                                break;
                            case "part":
                            case "leave":
                                PartChannel(args);
                                bAdminCommand = true;
                                break;
                            case "say":
                                int argDelim = args.IndexOf(' ');
                                string sendTo = args.Substring(0, argDelim);
                                string messageToSend = args.Substring(argDelim + 1);
                                SendMessage(sendTo, messageToSend); 
                                bAdminCommand = true;
                                break;
                            case "quit":
                                DisconnectOnExit();
                                Environment.Exit(0);
                                bAdminCommand = true;
                                break;
                        }

                    }

                    if (NoticeEvent != null && !bAdminCommand)
                    {
                        string noticeMessage = serverMessage.Params.Substring(serverMessage.Params.IndexOf(':') + 1);
                        NoticeType type = serverMessage.Command == "NOTICE" ? NoticeType.NOTICE : NoticeType.WHISPER;
                        NoticeEvent(type, userSender, noticeMessage);
                    }
                }
                catch (System.Exception ex)
                {
                    SendMessage(userSender, "something broke");
                    SendMessage(userSender, ex.Message.Substring(0, Math.Min(400, ex.Message.Length)));

                    Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                }

            }
        }

        private void HandleInvite(ServerMessage message)
        {
            if (Context.IsAuthenticatedUser(message.Prefix))
            {
                string channelName = message.Params.Substring(message.Params.IndexOf(':') + 1);
                JoinChannel(channelName);
            }
        }

        private void HandleChannelJoin(string channel)
        {
            Context.HandleChannelJoin(channel);
        }

        private void HandleUserJoinPart(ServerMessage serverMessage)
        {
            int startIndex = serverMessage.Prefix[0] == ':' ? 1 : 0;
            int endIndex = serverMessage.Prefix.IndexOf('!');

            string joiningUser = serverMessage.Prefix.Substring(startIndex, endIndex - startIndex);

            JoinType type = JoinType.Invalid;
            
            switch(serverMessage.Command)
            {
                case "JOIN":
                    type = JoinType.Join;
                    break;
                case "PART":
                    type = JoinType.Part;
                    break;

                default:
                    Fatty.PrintToScreen("Invalid Join Type");
                    break;
            }

            if (joiningUser.ToLower() == Context.Nick.ToLower())
            {
                if (type == JoinType.Join)
                {
                    // this handleuserjoinpart function is doing too many things, should refactor this at some point.
                    int channelStartIndex = serverMessage.Params[0] == ':' ? 1 : 0;
                    string joiningChannel = serverMessage.Params.Substring(channelStartIndex);
                    HandleChannelJoin(joiningChannel);
                }
            }
            else
            {
                Context.HandleUserJoinChannel(joiningUser, serverMessage.Params, type);
            }
        }

        private void HandlePing(ServerMessage response)
        {
            SendServerMessage("PONG " + response.Params.Substring(1));
        }

        private void HandleNameList(string[] commandTokens)
        {
            lock(NamesLock)
            {
                if (WaitingOnNames)
                {

                }
            }
        }

        private void HandleUserState(ServerMessage responseMessage, UserStateType type)
        {
            try
            {
                string channelName = responseMessage.Params;
                string displayName;
                if (type == UserStateType.User)
                {
                    displayName = responseMessage.Tags["display-name"];
                }
                else if (type == UserStateType.Room)
                {
                    displayName = responseMessage.Params;
                }
                else
                {
                    displayName = responseMessage.Params;
                }
            
                Context.HandleUserstate(type, responseMessage.Tags, channelName, displayName);
            }
            catch (Exception e)
            {
                Fatty.PrintWarningToScreen(e);
            }
        }

        private void OnWelcomeComplete()
        {
            SendMessage("nickserv", $"IDENTIFY {Context.NickAuthPassword}");

            Context.Channels.ForEach((channelContext) => { JoinChannel(channelContext.ChannelName); });
        }

        private void RegisterEventCallbacks()
        {
            this.IRCWelcomeProgress = new WelcomeProgress();
            IRCWelcomeProgress.WelcomeCompleteEvent += OnWelcomeComplete;
        }

        private void ListenForDisconnect()
        {
            Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            while(true)
            {
                Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);

                if (!IsConnectedToServer())
                {
                    ConnectToServer();
                    Thread.Sleep((int)TimeSpan.FromSeconds(60).Milliseconds);
                }
            }
        }

        private void HandleUserNotice(ServerMessage responseMessage)
        {
            int separatorIndex = responseMessage.Params.IndexOf(" ");
            string channel;
            string message = "";
            if (separatorIndex < 0)
            {
                channel = responseMessage.Params;
            }
            else
            {
                channel = responseMessage.Params.Substring(0, separatorIndex);
                message = responseMessage.Params.Substring(separatorIndex + 1);
            }

            Context.HandleUserNotice(responseMessage.Tags, channel, message);
        }

    }
}

