using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Json;
using System.Runtime.Serialization.Json;

namespace Fatty
{
    class Program
    {
        // need more than one
        static IRCConnection Irc;

        static void Main(string[] args)
        {
            StreamReader sr = new StreamReader("Connections.cfg");
            string connectionsString = sr.ReadToEnd();
            JsonValue connectionValue = JsonValue.Parse(connectionsString);

            // testing stuff
            //Console.WriteLine(connectionValue["ServerContext"][0]["ServerURL"]);

            JsonValue contextValue = connectionValue["ServerContext"][0];
            string ContextString = contextValue.ToString();

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(ContextString)))
            {
                var serializer = new DataContractJsonSerializer(typeof(ServerContext));
                ServerContext context = (ServerContext)serializer.ReadObject(ms);
                Irc = new IRCConnection(context);
                Irc.ConnectToServer();
            }

            //ServerContext context = new ServerContext();
            //context.ServerURL = contextValue["ServerURL"];
            //context.ServerPort = contextValue["ServerPort"];

            //Irc = new IRCConnection(context);
            //Irc.ConnectToServer();
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            if(Irc != default(IRCConnection))
            {
                Irc.DisconnectOnExit();
            }
        }
    }
}
