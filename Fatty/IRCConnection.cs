using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fatty
{
    #region Delegates
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
    #endregion

    class IRCConnection
    {
        public ServerContext Context { get; set; }

        private TcpClient IrcConnection { get; set; }
        private NetworkStream IrcStream { get; set; }
        private StreamWriter IrcWriter { get; set; }
        private StreamReader IrcReader { get; set; }

        private Object SendMessageLock = new Object();

        #region Events
        public event ChannelMessage eventChannelMessage;
        public event PrivateMessage eventPrivateMessage;
        public event TopicSet eventTopicSet;
        public event TopicOwner eventTopicOwner;
        public event NamesList eventNamesList;
        public event ServerMessage eventServerMessage;
        public event Join eventJoin;
        public event Part eventPart;
        public event Mode eventMode;
        public event NickChange eventNickChange;
        public event Kick eventKick;
        public event Quit eventQuit;
        public event Notice eventNotice;
        #endregion

        public IRCConnection(ServerContext context)
        {
            this.Context = context;
        }

        public void ConnectToServer()
        {
            Console.WriteLine("Attempting to connect to: {0}:{1}", Context.ServerURL, Context.ServerPort);

            try
            {
                //Establish connection
                this.IrcConnection = new TcpClient(Context.ServerURL, Context.ServerPort);
                this.IrcConnection.ReceiveTimeout = 1000 * 60 * 5;
                this.IrcStream = this.IrcConnection.GetStream();
                this.IrcReader = new StreamReader(this.IrcStream);
                this.IrcWriter = new StreamWriter(this.IrcStream);
                Console.WriteLine("Connection Sucessful");

                // Spawn listener Thread
                Thread th = new Thread(new ThreadStart(ListenForServerMessages));
                th.Start();

                // Send user info
                Console.WriteLine("Sending user info...");
                SendServerMessage(String.Format("USER {0} {0} {0} * :{1}", Context.Nick, Context.RealName));
                SendServerMessage(String.Format("NICK {0}", Context.Nick));
            }
            catch(Exception e)
            {
                Console.WriteLine("Connection Failed: {0}", e.Message);
            }
        }

        public void SendServerMessage(string message)
        {
            lock (SendMessageLock)
            {
                this.IrcWriter.WriteLine("{0}\r\n", message);
                this.IrcWriter.Flush();
            }
        }

        private void ListenForServerMessages()
        {
            string ircCommand;
            while ((ircCommand = this.IrcReader.ReadLine()) != null)
            {
                Console.WriteLine(ircCommand);
            }
        }
    }
}
