using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Net;
using System.Net.Mail;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Fatty
{
    class Fatty
    {
        public static IList<Type> GetDefaultModuleTypes { get { return DefaultModuleTypes.AsReadOnly(); } }
        public static IList<Type> GetModuleTypes { get { return ModuleTypes.AsReadOnly(); } }

        private IRCConnection Irc;

        private static List<Type> DefaultModuleTypes = new List<Type>();
        private static List<Type> ModuleTypes = new List<Type>();

        static private Object EmailLock = new object();
        static private EmailConfig EmailSettings;
        static private bool IsEmailConfigured = false;

        public void Launch()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            DefaultModuleTypes.Add(typeof(TalkBackModule));
            ModuleTypes.Add(typeof(TalkBackModule));
            ModuleTypes.Add(typeof(TDAmeritradeModule));
            ModuleTypes.Add(typeof(EmailModule));

            // Todo: loop through server contexts and connect to each
            ServerContext context = LoadServerConfig();
            EmailSettings = LoadEmailConfig();

            Irc = new IRCConnection(context);

            context.Initialize(Irc);
            Irc.ConnectToServer();

        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            if (Irc != default(IRCConnection))
            {
                Irc.DisconnectOnExit();
            }
        }

        private ServerContext LoadServerConfig()
        {
            try
            {
                StreamReader sr = new StreamReader("Connections.cfg");
                string connectionsString = sr.ReadToEnd();
                sr.Close();

                JsonValue connectionValue = JsonValue.Parse(connectionsString);

                JsonValue contextValue = connectionValue["ServerContext"][0];
                string ContextString = contextValue.ToString();

                MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(ContextString));

                var serializer = new DataContractJsonSerializer(typeof(ServerContext));
                ServerContext context;
                context = (ServerContext)serializer.ReadObject(ms);

                return context;
            }
            catch (Exception e)
            {
                Console.WriteLine("Invalid Connections Config: " + e.Message);
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
                        Console.WriteLine("Error: " + e.Message);
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
                return null;
            }
        }

    }
}
