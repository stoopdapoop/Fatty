using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
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

        public event ChannelMessageDelegate ChannelMessageEvent;
        public event PrivateMessageDelegate PrivateMessageEvent;
        public event TopicSetDelgate TopicSetEvent;
        public event TopicOwnerMessageDelegate TopicOwnerEvent;
        public event NamesListMessageDelegate NamesListEvent;
        public event ServerMessageDelegate ServerMessageEvent;
        public event JoinMessageDelegate JoinEvent;
        public event PartMessageDelegate PartEvent;
        public event ModeMessageDelegate ModeEvent;
        public event NickChangeMessageDelegate NickChangeEvent;
        public event KickMessageDelegate KickEvent;
        public event QuitMessageDelegate QuitEvent;
        public event NoticeDelegate NoticeEvent;
        public event ServerWelcome ServerWelcomeEvent;

        public IRCConnection(ServerContext context)
        {
            this.Context = context;
            this.IRCWelcomeProgress = new WelcomeProgress();
        }

        public void ConnectToServer()
        {
            Console.WriteLine("Attempting to connect to: {0}:{1}", Context.ServerURL, Context.ServerPort);

            RegisterEventCallbacks();

            try
            {
                //Establish connection
                this.IrcConnection = new TcpClient(Context.ServerURL, Context.ServerPort);
                this.IrcConnection.ReceiveTimeout = 1000 * 60 * 5;
                this.IrcStream = this.IrcConnection.GetStream();
                this.IrcReader = new StreamReader(this.IrcStream);
                this.IrcWriter = new StreamWriter(this.IrcStream);
                PrintToScreen("Connection Successful");

                // Spawn listener Thread
                Thread th = new Thread(new ThreadStart(ListenForServerMessages));
                th.Start();

                // Send user info
                PrintToScreen("Sending user info...");
                SendServerMessage(String.Format("NICK {0}", Context.Nick));
                SendServerMessage(String.Format("USER {0} 0 * :{1}", Context.Nick, Context.RealName));
            }
            catch(Exception e )
            {
                PrintToScreen("Connection Failed: {0}", e.Message);
            }
        }


        public void SendMessage(string sendTo, string message)
        {
            lock (WriteLock)
            {
                string outputMessage = String.Format("PRIVMSG {0} :{1}\r\n", sendTo, message);
                SendServerMessage(outputMessage);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(outputMessage);
                Console.ResetColor();
            }
        }

        private void SendServerMessage(string format, params object[] args)
        {
            SendServerMessage(String.Format(format, args));
        }

        private void SendServerMessage(string message)
        {
            lock (WriteLock)
            {
                this.IrcWriter.WriteLine("{0}\r\n", message);
                this.IrcWriter.Flush();
            }
        }

        public void DisconnectOnExit()
        {
            PrintToScreen("Disconnecting Due to Exit");
            SendServerMessage(String.Format("QUIT {0}", Context.QuitMessage));
        }

        private void ListenForServerMessages()
        {
            string ircResponse;
            while ((ircResponse = this.IrcReader.ReadLine()) != null)
            {
                PrintToScreen(ircResponse);

                DispatchMessageEvents(ircResponse);
            }
        }

        private void JoinChannel(string channelName)
        {
            SendServerMessage("JOIN {0}", channelName);
        }

        public void PrintToScreen(string format, params object[] args)
        {
            PrintToScreen(String.Format(format, args));
        }

        public void PrintToScreen(string message)
        {
            if (Context.ShouldPrintToScreen)
            {
                Console.WriteLine(message);
            }
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
            }
        }

        private void HandleWelcomeMessage(string[] tokens)
        {
            byte welcomeID = Byte.Parse(tokens[1]);
            IRCWelcomeProgress.NotifyOfMessage(welcomeID);
        }

        private void HandlePrivMsg(string[] tokens, string originalMessage)
        {
            if (ChannelMessageEvent == null && PrivateMessageEvent == null)
                return;

            string userSender = tokens[0].Substring(0, tokens[0].IndexOf('!'));
            string messageTo = tokens[2];
            string chatMessage = originalMessage.Substring(1 + originalMessage.LastIndexOf(':'));

            if(messageTo[0] == '#' || messageTo[0] == '&')
            {
                if (ChannelMessageEvent != null)
                {
                    foreach (ChannelMessageDelegate chanDel in ChannelMessageEvent.GetInvocationList())
                    {
                        Debug.Assert(Object.ReferenceEquals(chanDel.Target.GetType(), typeof(ChannelContext)), "Target of ChannelMessageDelegate not of type ChannelContext");

                        ChannelContext DelegateContext = (ChannelContext)chanDel.Target;
                        if (DelegateContext.ChannelName == messageTo)
                        {
                            chanDel.BeginInvoke(userSender, messageTo, chatMessage, null, null);
                        }
                    }
                }
            }
            else
            {
                if (PrivateMessageEvent != null)
                {
                    foreach (PrivateMessageDelegate privDel in PrivateMessageEvent.GetInvocationList())
                    {
                        privDel.BeginInvoke(userSender, chatMessage, null, null);
                    }
                }
            }
        }

        private void HandleNotice(string[] tokens)
        {
            if(NoticeEvent != null)
            {
                string userSender = tokens[0].Substring(0, tokens[0].IndexOf('!'));
                string noticeMessage = tokens[3].Substring(1);
                NoticeEvent(userSender, noticeMessage);
            }
        }


        private void HandlePing(string[] pingTokens)
        {
            string pingHash = pingTokens[1].Substring(1);
            SendServerMessage("PONG " + pingHash);
        }

        private void OnWelcomeComplete()
        {
            Context.Channels.ForEach((channelContext) => { JoinChannel(channelContext.ChannelName); });
        }

        private void RegisterEventCallbacks()
        {
            IRCWelcomeProgress.WelcomeCompleteEvent += OnWelcomeComplete;
            //ChannelMessageEvent += TestOnChannelMessage;
        }

        private void TestOnChannelMessage(string ircUser, string ircChannel, string message)
        {
            if (ircChannel != "#cuties")
                return;
            SmtpClient testMail = new SmtpClient("smtp.gmail.com", 587);
            testMail.UseDefaultCredentials = false;
            testMail.Credentials = new NetworkCredential("sirragnard@gmail.com", "");
            testMail.EnableSsl = true;
            
            MailMessage testMessage = new MailMessage("sirragnard@gmail.com", "@vtext.com", "hullo", message);
            testMail.Send(testMessage);
        }
    }
}

