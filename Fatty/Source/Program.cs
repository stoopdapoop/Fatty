using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Fatty
{
    class Program
    {
        // need more than one
        static IRCConnection Irc;

        static List<FattyModule> Modules = new List<FattyModule>();

        static void Main(string[] args)
        {
            StreamReader sr = new StreamReader("Connections.cfg");
            string connectionsString = sr.ReadToEnd();
            JsonValue connectionValue = JsonValue.Parse(connectionsString);

            JsonValue contextValue = connectionValue["ServerContext"][0];
            string ContextString = contextValue.ToString();

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            Modules.Add(new TalkBackModule());

            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(ContextString)))
            {
                var serializer = new DataContractJsonSerializer(typeof(ServerContext));
                ServerContext context = (ServerContext)serializer.ReadObject(ms);
                Irc = new IRCConnection(context);

                RegisterModuleCallbacks(Irc);
                
                Irc.ConnectToServer();
            }
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            if(Irc != default(IRCConnection))
            {
                Irc.DisconnectOnExit();
            }
        }

        static void RegisterModuleCallbacks(IRCConnection connection)
        {
            foreach (FattyModule mod in Modules )
            {
                mod.RegisterEvents(connection);
            }
        }
    }
}
