using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Fatty
{
    class EmailModule : FattyModule
    {
        static private Object EmailLock = new object();
        private EmailConfig EmailSettings;
        private bool IsEmailConfigured = false;

        public EmailModule()
        {

        }

        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);

            EmailSettings = LoadEmailConfig();
        }

        public override void RegisterAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("Email", EmailCommand, @"params are : [EmailAddress] [Message]"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("Email");
        }

        public override void RegisterEvents()
        {
            base.RegisterEvents();
        }

        void EmailCommand(string ircUser, string ircChannel, string message)
        {
            string[] chunks = message.Split(" ");

            if (chunks.Length > 2)
                SendEmail(chunks[1], ircUser, String.Join(" ", chunks, 2, chunks.Length - 2));
        }

        void SendEmail(string to, string from, string message)
        {
            bool success = SendEmailToClient(to, String.Format("A message from {0} in {1}", from, OwningChannel.ChannelName), message);
            if(success)
            {
                OwningChannel.SendMessage(String.Format("sent \"{0}\" to {1}", message, to), from);
            }
            else
            {
                OwningChannel.SendMessage(String.Format("failed to send \"{0}\" to {1}", message, to), from);
            }
        }

        public bool SendEmailToClient(string recipient, string subject, string message)
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
                        Fatty.PrintWarningToScreen($"Error sending mail from address {EmailSettings.EmailAddress} - {e.Message}", e.StackTrace);
                        Fatty.PrintToScreen(e.StackTrace, ConsoleColor.Yellow);
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
                Fatty.PrintWarningToScreen($"Error loading mail config: {e.Message}", e.StackTrace);
                return default(EmailConfig);
            }
        }
    }
}
