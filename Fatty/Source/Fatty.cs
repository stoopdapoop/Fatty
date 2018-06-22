using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    class Fatty
    {
        public static IList<Type> GetDefaultModuleTypes { get { return DefaultModuleTypes.AsReadOnly(); } }
        public static IList<Type> GetModuleTypes { get { return ModuleTypes.AsReadOnly(); } }

        private IRCConnection Irc;

        private static List<Type> DefaultModuleTypes = new List<Type>();
        private static List<Type> ModuleTypes = new List<Type>();

        public void Launch()
        {
            StreamReader sr = new StreamReader("Connections.cfg");
            string connectionsString = sr.ReadToEnd();
            JsonValue connectionValue = JsonValue.Parse(connectionsString);

            JsonValue contextValue = connectionValue["ServerContext"][0];
            string ContextString = contextValue.ToString();

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            DefaultModuleTypes.Add(typeof(TalkBackModule));
            ModuleTypes.Add(typeof(TalkBackModule));

            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(ContextString)))
            {
                var serializer = new DataContractJsonSerializer(typeof(ServerContext));
                ServerContext context = (ServerContext)serializer.ReadObject(ms);

                Irc = new IRCConnection(context);

                context.Initialize(Irc);

                RegisterModuleCallbacks();

                Irc.ConnectToServer();
            }
        }

        void OnProcessExit(object sender, EventArgs e)
        {
            if (Irc != default(IRCConnection))
            {
                Irc.DisconnectOnExit();
            }
        }


        void RegisterModuleCallbacks()
        {
            //foreach (FattyModule mod in Modules)
            //{
            //    mod.RegisterEvents();
            //}
        }
    }
}
