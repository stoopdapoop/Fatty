using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Reflection;
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
            Console.CancelKeyPress += OnProcessExit;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            ModuleTypes = FattyHelpers.GetAllDerivedClasses<FattyModule>();

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
                Fatty.PrintWarningToScreen($"Invalid Connections Config: {e.Message}", e.StackTrace);
                return null;
            }
        }

        public static void PrintToScreen(string format, params object[] args)
        {
            PrintToScreen(String.Format(format, args));
        }

        public static void PrintWarningToScreen(Exception ex)
        {
            PrintWarningToScreen(ex.Message, ex.StackTrace);
        }

        public static void PrintWarningToScreen(string message, string optionalStack = "")
        {
            lock (PrintLock)
            {
                PrintToScreen("-----------------------------------------------------------------------------\r\n");
                PrintToScreen(message, ConsoleColor.Yellow);
                if (optionalStack.Length > 0)
                {
                    PrintToScreen(optionalStack, ConsoleColor.Yellow);
                }
                PrintToScreen("-----------------------------------------------------------------------------\r\n");
            }
        }

        public static void PrintToScreen(string message)
        {
            // data race on foreground color, o well
            PrintToScreen(message, Console.ForegroundColor);
        }

        public static void PrintToScreen(string message, ConsoleColor color)
        {
            string output = $"[{DateTime.Now}] {new string(message.Where(c => !char.IsControl(c)).ToArray())}";
            lock (PrintLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(output.TrimEnd());
                Console.ResetColor();
            }
            Logger.Log(output);
        }
    }

    static class Logger
    {
        static private readonly string FilePath;
        static private Object LogLock = new object();

        static Logger()
        {
            FilePath = $"logs/log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        }

        static public void Log(string message)
        {
            lock (LogLock)
            {
                File.AppendAllText(FilePath, $"{message}{Environment.NewLine}");
            }
        }
    }
}
