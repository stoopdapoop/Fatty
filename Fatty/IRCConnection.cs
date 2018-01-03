using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    class IRCConnection
    {
        public ServerContext Context { get; set; }

        private TcpClient IrcConnection { get; set; }
        private NetworkStream IrcStream { get; set; }
        private StreamWriter IrcWriter { get; set; }
        private StreamReader IrcReader { get; set; }

        public IRCConnection(ServerContext context)
        {
            this.Context = context;
        }

        public void ConnectToServer()
        {
            this.IrcConnection = new TcpClient(Context.ServerURL, Context.ServerPort);
            this.IrcConnection.ReceiveTimeout = 1000 * 60 * 5;
            this.IrcStream = this.IrcConnection.GetStream();
            this.IrcReader = new StreamReader(this.IrcStream);
            this.IrcWriter = new StreamWriter(this.IrcStream);
        }
    }
}
