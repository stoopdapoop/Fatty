using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Timers;

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

        public event PrivateMessageDelegate PrivateMessageEvent;
        public event NoticeDelegate NoticeEvent;

        public IRCConnection(ServerContext context)
        {
            this.Context = context;
        }

        private void SendNickAndUser(string nick, string user)
        {
            SendServerMessage($"NICK {nick}");
            SendServerMessage($"USER {nick} 0 * :{user}");
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
                if(Context.ServerAuthPassword != null)
                {
                    SendServerMessage($"Pass {Context.ServerAuthPassword}");
                }
                SendNickAndUser(Context.Nick, Context.RealName);

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
            string outputMessage = String.Format("PRIVMSG {0} :{1}", sendTo, message);
            SendServerMessage(outputMessage);
        }

        public void SendNotice(string sendTo, string message)
        {
            string outputMessage = String.Format("NOTICE {0} :{1}", sendTo, message);
            SendServerMessage(outputMessage);
        }

        public void SendCapRequest(string cap)
        {
            SendServerMessage($"CAP REQ :{cap}");
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
            Fatty.PrintToScreen("Disconnecting Due to Exit");
            SendServerMessage(String.Format("QUIT :{0}", Context.QuitMessage));
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

            Fatty.PrintToScreen("Listener Died", ConsoleColor.Yellow);
            ListenerIsListening = false;
        }

        // todo, use n'th index of space for substring instead of token join
        private void PrintServerMessage(string message)
        {
            if (message.StartsWith("PING"))
                return;

            string[] messageTokens = message.Split(' ');

            if (messageTokens[1] == "PRIVMSG")
            {
                string talkingUser = messageTokens[0].Substring(0, messageTokens[0].IndexOf('!')).TrimStart(':');
                string userMessage = String.Join(' ', messageTokens, 3, messageTokens.Length - 3);
                // unsure if this check is needed but as far as I can tell all messages start with this colon, but it's not explicity specified in the RFC
                if (userMessage[0] == ':')
                {
                    userMessage = userMessage.Substring(1, userMessage.Length - 1);
                }

                Fatty.PrintToScreen(String.Format("{0}<{1}>{2}", messageTokens[2], talkingUser, userMessage), ConsoleColor.DarkCyan);
            }
            else if (messageTokens[1].Length == 3 && Char.IsDigit(messageTokens[1][0]))
            {
                string serverMessage = String.Join(' ', messageTokens, 3, messageTokens.Length - 3).TrimStart(':');
                Fatty.PrintToScreen(String.Format("{0}:{1}:{2}", Context.ServerName, messageTokens[1], serverMessage), ConsoleColor.White);
            }
            else if(messageTokens[1] == "NOTICE")
            {
                string noticeSender = messageTokens[0];
                string noticeMessage = String.Join(' ', messageTokens, 3, messageTokens.Length - 3).TrimStart(':');
                Fatty.PrintToScreen(String.Format("{0}:NOTICE {1}", noticeSender, noticeMessage), ConsoleColor.DarkRed);
            }
            else if(messageTokens[1] == "JOIN" || messageTokens[1] == "PART")
            {
                bool bIsJoin = messageTokens[1] == "JOIN";
                int startIndex = messageTokens[0][0] == ':' ? 1 : 0;
                int endIndex = messageTokens[0].IndexOf('!');
                string joiningUser = messageTokens[0].Substring(startIndex, endIndex - startIndex);

                Fatty.PrintToScreen($"{messageTokens[2]}<{joiningUser}> {messageTokens[1]}", bIsJoin ? ConsoleColor.Cyan : ConsoleColor.DarkYellow);
            }
            else
            {
                Fatty.PrintToScreen(message);
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
            SendServerMessage("JOIN {0}", channelName);
        }

        private void PartChannel(string channelName)
        {
            SendServerMessage("PART {0} :{1}", channelName, Context.QuitMessage);
        }

        private void DispatchMessageEvents(string response)
        {
            string[] commandTokens = response.Split(' ');
            if (commandTokens[0][0] == ':')
                commandTokens[0] = commandTokens[0].Remove(0, 1);

            if (commandTokens[0] == "PING")
            {
                HandlePing(commandTokens);
            }

            switch (commandTokens[1])
            {
                // welcome messages
                case "001":
                case "002":
                case "003":
                case "004":
                    {
                        HandleWelcomeMessage(commandTokens);
                        break;
                    }
                case "PRIVMSG":
                    {
                        HandlePrivMsg(commandTokens, response);
                        break;
                    }
                case "NOTICE":
                    {
                        HandleNotice(commandTokens);
                        break;
                    }
                case "INVITE":
                    {
                        HandleInvite(commandTokens);
                        break;
                    }
                case "353":
                    {
                        HandleChannelJoin(commandTokens);
                        break;
                    }
                case "432":
                    {
                        SendMessage("nickserv", "IDENTIFY {Context.NickAuthPassword}");
                        break;
                    }
                case "JOIN":
                case "PART":
                    {
                        HandleUserJoinPart(commandTokens);
                        break;
                    }
            }
        }

        private void HandleWelcomeMessage(string[] tokens)
        {
            int welcomeID =  int.Parse(tokens[1]);
            IRCWelcomeProgress.NotifyOfMessage(welcomeID);
        }

        private void HandlePrivMsg(string[] tokens, string originalMessage)
        {
            string userSender = tokens[0].Substring(0, tokens[0].IndexOf('!'));
            string messageTo = tokens[2];
            string chatMessage = originalMessage.Substring(1 + originalMessage.IndexOf(':', 1));
            
            if (messageTo[0] == '#' || messageTo[0] == '&')
            {
                Context.HandleServerMessage(userSender, messageTo, chatMessage);
            }
            else if (chatMessage.StartsWith('\u0001') && chatMessage.EndsWith('\u0001'))
            {
                string trimmedMessage = chatMessage.Trim('\u0001');
                string[] trimmedChunks = trimmedMessage.Split();
                switch(trimmedChunks[0])
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

        private void HandleNotice(string[] tokens)
        {
            bool bAdminCommand = false;
            try
            {
                if (IsAuthenticatedUser(tokens[0]))
                {
                    if (tokens.Length < 5)
                    {
                        string userSender = tokens[0].Substring(0, tokens[0].IndexOf('!'));
                        SendMessage(userSender, "Not enough args");
                    }
                    else
                    {
                        string messageCommand = tokens[3].TrimStart(':').ToLower();
                        switch (messageCommand)
                        {
                            case "join":
                                RuntimeJoinChannel(tokens[4]);
                                bAdminCommand = true;
                                break;
                            case "part":
                            case "leave":
                                PartChannel(tokens[4]);
                                bAdminCommand = true;
                                break;
                            case "say":
                                string sendTo = tokens[4];
                                SendMessage(sendTo, String.Join(" ", tokens, 5, tokens.Length - 5)); ;
                                bAdminCommand = true;
                                break;
                            case "quit":
                                DisconnectOnExit();
                                Environment.Exit(0);
                                bAdminCommand = true;
                                break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                string userSender = tokens[0].Substring(0, tokens[0].IndexOf('!'));
                SendMessage(userSender, "something broke");
                Thread.Sleep(500);
                SendMessage(userSender, ex.Message.Substring(0, 400));

                Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
            }

            if (NoticeEvent != null && !bAdminCommand)
            {
                string userSender = tokens[0].Substring(0, tokens[0].IndexOf('!'));
                string noticeMessage = tokens[3].TrimStart(':');
                NoticeEvent(userSender, noticeMessage);
            }
        }

        private void HandleInvite(string[] tokens)
        {
            bool ChannelJoined = false;
            if (IsAuthenticatedUser(tokens[0]))
            {
                JoinChannel(tokens[3].TrimStart(':'));
                ChannelJoined = true;
            }

            if (!ChannelJoined)
            {
                int startIndex = tokens[0][0] == ':' ? 1 : 0;
                int endIndex = tokens[0].IndexOf('!');
                string SendingTo = tokens[0].Substring(startIndex, endIndex - startIndex);
                SendMessage(SendingTo, "Nope, Sorry");
            }
        }

        private void HandleChannelJoin(string[] tokens)
        {
            Context.HandleChannelJoin(tokens[4]);
        }

        private void HandleUserJoinPart(string[] tokens)
        {
            int startIndex = tokens[0][0] == ':' ? 1 : 0;
            int endIndex = tokens[0].IndexOf('!');

            string joiningUser = tokens[0].Substring(startIndex, endIndex - startIndex);

            JoinType type = JoinType.Invalid;
            
            switch(tokens[1])
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

            if (joiningUser != Context.Nick.ToLower())
            {
                Context.HandleUserJoinChannel(joiningUser, tokens[2], type);
            }
        }

        private void HandlePing(string[] pingTokens)
        {
            string pingHash = pingTokens[1].Substring(1);
            SendServerMessage("PONG " + pingHash);
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

        private bool IsAuthenticatedUser(string UserToken)
        {
            foreach (string authMask in Context.AuthenticatedMasks)
            {
                if (UserToken.Substring(UserToken.IndexOf("@") + 1) == authMask)
                {
                    return true;
                }
            }

            return false;
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
                    Thread.Sleep((int)TimeSpan.FromSeconds(30).Milliseconds);
                }
            }
        }
    }
}

