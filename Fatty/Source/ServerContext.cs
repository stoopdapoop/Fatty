using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class ServerContext
    {
        [DataMember(IsRequired = true)]
        public string ServerURL { get; private set; }

        [DataMember(IsRequired = true)]
        public UInt16 ServerPort { get; private set; }

        [DataMember(IsRequired = true)]
        public string Nick { get; private set; }

        [DataMember(IsRequired = true)]
        public string RealName { get; private set; } = "FattyBot";

        [DataMember(IsRequired = true)]
        public bool ShouldPrintToScreen { get; private set; }

        [DataMember(IsRequired = true)]
        public string CommandPrefix { get; private set; }

        [DataMember]
        public string AuthPassword { get; private set; }

        [DataMember]
        public List<ChannelContext> Channels { get; private set; }

        [DataMember]
        public List<string> AuthenticatedMasks { get; private set; }

        [DataMember]
        public string QuitMessage { get; private set; }

        public event ChannelMessageDelegate ChannelMessageEvent;

        public event ChannelJoinedDelegate ChannelJoinedEvent;

        private IRCConnection OwnerConnection { get; set; }

        private LoggingContext Logging;

        public void Initialize(IRCConnection irc)
        {
            OwnerConnection = irc;
            InitLogging();

            foreach (ChannelContext context in Channels)
            {
                context.Initialize(this);
            }
        }

        public void HandleChannelJoin(string ircChannel)
        {
            if (ChannelJoinedEvent != null)
            {
                foreach (ChannelJoinedDelegate chanDel in ChannelJoinedEvent.GetInvocationList())
                {
                    Debug.Assert(Object.ReferenceEquals(chanDel.Target.GetType(), typeof(ChannelContext)), "Target of ChannelMessageDelegate not of type ChannelContext");

                    ChannelContext DelegateContext = (ChannelContext)chanDel.Target;
                    if (DelegateContext.ChannelName == ircChannel)
                    {
                        chanDel(ircChannel);
                    }
                }
            }
        }

        public void HandleServerMessage(string ircUser, string ircChannel, string message)
        {
            IrcLogUser FoundUser;
            var UserQuery = Logging.Users.Where(x => x.Nick == ircUser);
            if (UserQuery.Count() == 0)
            {
                IrcLogUser logUser = new IrcLogUser(ircUser);
                logUser.UserId = 0;
                Logging.Users.Add(logUser);
                Logging.SaveChanges();
                FoundUser = logUser;
            }
            else
            {
                FoundUser = UserQuery.First();
            }

            var MessageLog = new ChannelMessageLog();
            MessageLog.ChannelName = ircChannel;
            MessageLog.User = FoundUser;
            MessageLog.Message = message;
            //MessageLog.Date = DateTime.Now.ToString("YYYY-MM-DD HH:MM:SS.SSS");
            MessageLog.Date = DateTime.Now;
            Logging.Messages.Add(MessageLog);
            Logging.SaveChanges();

            if (ChannelMessageEvent != null)
            {
                foreach (ChannelMessageDelegate chanDel in ChannelMessageEvent.GetInvocationList())
                {
                    Debug.Assert(Object.ReferenceEquals(chanDel.Target.GetType(), typeof(ChannelContext)), "Target of ChannelMessageDelegate not of type ChannelContext");

                    ChannelContext DelegateContext = (ChannelContext)chanDel.Target;
                    if (DelegateContext.ChannelName == ircChannel)
                    {
                        chanDel(ircUser, ircChannel, message);
                    }
                }
            }
        }

        public void SendMessage(string ircChannel, string message)
        {
            OwnerConnection.SendMessage(ircChannel, message);
        }

        private void InitLogging()
        {
            DbContextOptionsBuilder<LoggingContext> optionsBuilder = new DbContextOptionsBuilder<LoggingContext>();
            optionsBuilder.UseSqlite("Data Source=Logging.db");

            Logging = new LoggingContext(optionsBuilder.Options);
            Logging.Database.EnsureCreated();
        }
    }
}
