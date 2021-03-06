﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Fatty
{
    class Fatty
    {
        [DataContract]
        public class FattyContext
        {
            [DataMember(IsRequired = true, Name = "ServerContexts")]
            public List<ServerContext> Contexts { get; protected set; }
        }

        public static IList<Type> GetModuleTypes { get { return ModuleTypes.AsReadOnly(); } }

        private List<IRCConnection> IrcConnections = new List<IRCConnection>();

        private static List<Type> ModuleTypes = new List<Type>();

        static private Object PrintLock = new object();

        public void Launch()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            ModuleTypes.Add(typeof(TalkBackModule));
            ModuleTypes.Add(typeof(TDAmeritradeModule));
            ModuleTypes.Add(typeof(EmailModule));
            ModuleTypes.Add(typeof(TwitchModule));
            ModuleTypes.Add(typeof(GitHubModule));

            // Todo: loop through server contexts and connect to each
            List<ServerContext> ServerContexts = LoadServerConfigs();

            foreach (var server in ServerContexts)
            {
                IRCConnection currentConnection = new IRCConnection(server);
                IrcConnections.Add(currentConnection);

                server.Initialize(currentConnection);
                currentConnection.ConnectToServer();
            }
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            foreach (IRCConnection connection in IrcConnections)
            {
                if (connection != null)
                {
                    connection.DisconnectOnExit();
                }
            }
        }

        private List<ServerContext> LoadServerConfigs()
        {
            try
            {
                StreamReader sr = new StreamReader("Connections.cfg");
                string connectionsString = sr.ReadToEnd();
                sr.Close();

                JsonValue contextValue = JsonValue.Parse(connectionsString);
                string ContextString = contextValue.ToString();

                MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(ContextString));

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(FattyContext));
                FattyContext context = (FattyContext)serializer.ReadObject(ms);

                return context.Contexts;
            }
            catch (Exception e)
            {
                Fatty.PrintToScreen("Invalid Connections Config: " + e.Message);
                return null;
            }
        }

        public static void PrintToScreen(string format, params object[] args)
        {
            PrintToScreen(String.Format(format, args));
        }

        public static void PrintToScreen(string message)
        {
            lock (PrintLock)
            {
                string output = new string(message.Where(c => !char.IsControl(c)).ToArray());
                Console.WriteLine(output.TrimEnd());
            }
        }

        public static void PrintToScreen(string message, ConsoleColor color)
        {
            lock (PrintLock)
            {
                Console.ForegroundColor = color;
                string output = new string(message.Where(c => !char.IsControl(c)).ToArray());
                Console.WriteLine(output.TrimEnd());
                Console.ResetColor();
            }
        }
    }
}
