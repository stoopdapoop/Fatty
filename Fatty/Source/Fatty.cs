using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Net.Mail;
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

        private bool IsEmailConfigured = false;

        public void Launch()
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            DefaultModuleTypes.Add(typeof(TalkBackModule));
            ModuleTypes.Add(typeof(TalkBackModule));

            // Todo: loop through server contexts and connect to each
            ServerContext context = LoadServerConfig();

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
            catch(Exception e)
            {
                Console.WriteLine("Invalid Connections Config: " + e.Message);
                return null;
            }
        }

        public bool SendEmail()
        {
            if (IsEmailConfigured)
            {
                try
                {
                    SmtpClient testMail = new SmtpClient("smtp.gmail.com", 587);
                    testMail.UseDefaultCredentials = false;
                    testMail.Credentials = new NetworkCredential("sirragnard@gmail.com", "");
                    testMail.EnableSsl = true;

                    //MailMessage testMessage = new MailMessage("sirragnard@gmail.com", "@vtext.com", "hullo", message);
                    //testMail.Send(testMessage);
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
}
