using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Net;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;
using System.Threading;

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

        private IRCConnection Irc;

        private static List<Type> ModuleTypes = new List<Type>();

        static private Object EmailLock = new object();
        static private EmailConfig EmailSettings;
        static private bool IsEmailConfigured = false;

        static private Object PrintLock = new object();

        public void Launch()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            ModuleTypes.Add(typeof(TalkBackModule));
            ModuleTypes.Add(typeof(TDAmeritradeModule));
            ModuleTypes.Add(typeof(EmailModule));
            ModuleTypes.Add(typeof(TwitchModule));

            EmailSettings = LoadEmailConfig();
            // Todo: loop through server contexts and connect to each
            List<ServerContext> ServerContexts = LoadServerConfigs();

            foreach (var server in ServerContexts)
            {
                Irc = new IRCConnection(server);

                server.Initialize(Irc);
                Irc.ConnectToServer();
            }
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            if (Irc != null)
            {
                Irc.DisconnectOnExit();
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

        static public bool SendEmail(string recipient, string subject, string message)
        {
            lock (EmailLock)
            {
                if (IsEmailConfigured)
                {
                    try
                    {
                        SmtpClient MailClient = new SmtpClient(EmailSettings.SMTPAddress, EmailSettings.SMTPPort);
                        MailClient.UseDefaultCredentials = false;
                        MailClient.Credentials = new NetworkCredential(EmailSettings.EmailAddress, EmailSettings.Password);
                        MailClient.EnableSsl = true;

                        MailMessage MessageToSend = new MailMessage(EmailSettings.EmailAddress, recipient, subject, message);
                        MailClient.Send(MessageToSend);
                    }
                    catch (Exception e)
                    {
                        Fatty.PrintToScreen("Error: " + e.Message);
                        return false;
                    }

                    return true;
                }
                return false;
            }
        }

        private EmailConfig LoadEmailConfig()
        {
            try
            {
                var ReturnConfig = FattyHelpers.DeserializeFromPath<EmailConfig>("EmailConfig.cfg");
                IsEmailConfigured = true;
                return ReturnConfig;
            }
            catch (Exception e)
            {
                Fatty.PrintToScreen(e.Message);
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
