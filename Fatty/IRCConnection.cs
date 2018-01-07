using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Fatty
{
    #region Delegates
    public delegate void AnyMessage(string message);
    public delegate void ChannelMessage(string ircUser, string ircChannel, string message);
    public delegate void PrivateMessage(string ircUser, string message);
    public delegate void TopicSet(string ircChannel, string ircTopic);
    public delegate void TopicOwner(string ircChannel, string ircUser, string topicDate);
    public delegate void NamesList(string userNames);
    public delegate void ServerMessage(string serverMessage);
    public delegate void Join(string ircChannel, string ircUser);
    public delegate void Part(string ircChannel, string ircUser);
    public delegate void Mode(string ircChannel, string ircUser, string userMode);
    public delegate void NickChange(string UserOldNick, string UserNewNick);
    public delegate void Kick(string ircChannel, string userKicker, string userKicked, string kickMessage);
    public delegate void Quit(string userQuit, string quitMessage);
    public delegate void Notice(string ircUser, string message);
    public delegate void ServerWelcome(int messageID);
    #endregion

    partial class IRCConnection
    {
        public ServerContext Context { get; set; }

        private TcpClient IrcConnection { get; set; }
        private NetworkStream IrcStream { get; set; }
        private StreamWriter IrcWriter { get; set; }
        private StreamReader IrcReader { get; set; }

        private WelcomeProgress IRCWelcomeProgress;

        #region Events
        public event ChannelMessage ChannelMessageEvent;
        public event PrivateMessage PrivateMessageEvent;
        public event TopicSet TopicSetEvent;
        public event TopicOwner TopicOwnerEvent;
        public event NamesList NamesListEvent;
        public event ServerMessage ServerMessageEvent;
        public event Join JoinEvent;
        public event Part PartEvent;
        public event Mode ModeEvent;
        public event NickChange NickChangeEvent;
        public event Kick KickEvent;
        public event Quit QuitEvent;
        public event Notice NoticeEvent;
        public event AnyMessage AnyMessageEvent;
        public event ServerWelcome ServerWelcomeEvent;
        #endregion

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

        public void SendServerMessage(string format, params object[] args)
        {
            SendServerMessage(String.Format(format, args));
        }

        public void SendServerMessage(string message)
        {
            this.IrcWriter.WriteLine("{0}\r\n", message);
            this.IrcWriter.Flush();
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
                        byte welcomeID = Byte.Parse(commandTokens[1]);
                        IRCWelcomeProgress.NotifyOfMessage(welcomeID);
                        break;
                    }                   
            }
        }

        private void HandlePing(string[] pingTokens)
        {
            string pingHash = "";
            pingHash = String.Join(" ", pingTokens, 1, pingTokens.Length - 1);
            SendServerMessage("PONG " + pingHash);
        }

        private void OnWelcomeComplete()
        {
            Context.Channels.ForEach((channelName) => { JoinChannel(channelName); });
        }

        private void RegisterEventCallbacks()
        {
            IRCWelcomeProgress.WelcomeCompleteEvent += OnWelcomeComplete;
        }
    }
}
