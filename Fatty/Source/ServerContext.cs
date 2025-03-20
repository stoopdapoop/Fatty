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
        public string ServerName { get; private set; }

        [DataMember(IsRequired = true)]
        public string ServerURL { get; private set; }

        [DataMember(IsRequired = true)]
        public UInt16 ServerPort { get; private set; }

        [DataMember(IsRequired = true)]
        public string Nick { get; private set; }

        [DataMember(IsRequired = true)]
        public string RealName { get; private set; } = "FattyBot";

        [DataMember(IsRequired = true)]
        public string CommandPrefix { get; private set; }

        [DataMember]
        public bool UseSSL { get; private set; }

        [DataMember]
        public string NickAuthPassword { get; private set; }

        [DataMember]
        public string ServerAuthPassword { get; private set; }

        [DataMember]
        public List<string> ServerCaps { get; private set; }

        [DataMember]
        public List<ChannelContext> Channels { get; private set; }

        [DataMember]
        public List<string> AuthenticatedMasks { get; private set; }

        [DataMember]
        public string QuitMessage { get; private set; }

        public event ChannelMessageDelegate ChannelMessageEvent;

        public event ChannelJoinedDelegate ChannelJoinedEvent;

        public event UserJoinPartDelegate UserJoinedEvent;

        public event UserstateDelegate UserstateEvent;

        public event UserNoticeDelegate UserNoticeEvent;

        private IRCConnection OwnerConnection { get; set; }

        // todo: clean up contexts when parting or getting kicked


        private Object LoggingLock;
        private LoggingContext Logging;
        private ServerLog ServerLogInstance;
        private Dictionary<string, ChannelLog> ChannelLogInstances;

        [OnDeserialized]
        private void DeserializationInitializer(StreamingContext ctx)
        {
            // lock init needs to happen here
            LoggingLock = new object();

            ChannelLogInstances = new Dictionary<string, ChannelLog>();

            if (AuthenticatedMasks == null)
                AuthenticatedMasks = new List<string>();
        }

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
            lock (LoggingLock)
            {
                ChannelLog JoiningLog;
                var ChannelQuery = Logging.Channels.AsQueryable().Where(x => x.ChannelName == ircChannel && x.Server == ServerLogInstance);
                if (ChannelQuery.Count() == 0)
                {
                    JoiningLog = new ChannelLog();
                    JoiningLog.ChannelName = ircChannel;
                    JoiningLog.Server = ServerLogInstance;
                    Logging.Channels.Add(JoiningLog);
                    Logging.SaveChanges();
                }
                else
                {
                    JoiningLog = ChannelQuery.First();
                }
                ChannelLogInstances.TryAdd(ircChannel, JoiningLog);
            }

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

        public void HandleServerMessage(Dictionary<string, string>? tags, string ircUser, string ircChannel, string message)
        {

            ChannelContext curContext = Channels.Find(x => x.ChannelName == ircChannel);
            bool ShouldLog = true;
            if (curContext != null)
            {
                ShouldLog = !curContext.LoggingDisabled;
            }
            if (ShouldLog)
            {
                lock (LoggingLock)
                {
                    try
                    {
                        IrcLogUser FoundUser = Logging.Users.Find(ircUser, ServerLogInstance.Id);
                        if (FoundUser == null)
                        {
                            FoundUser = new IrcLogUser(ircUser, ServerLogInstance.Id, DateTime.Now);
                            Logging.Users.Add(FoundUser);
                            // saves later
                        }
                        else
                        {
                            FoundUser.LastSeen = DateTime.Now;
                        }

                        var MessageLog = new ChannelMessageLog();
                        MessageLog.Channel = ChannelLogInstances[ircChannel];
                        MessageLog.User = FoundUser;
                        MessageLog.Message = message;
                        //MessageLog.Date = DateTime.Now.ToString("YYYY-MM-DD HH:MM:SS.SSS");
                        MessageLog.Date = DateTime.Now;
                        var entityEntry = Logging.Messages.Add(MessageLog);
                        Logging.SaveChanges();
                        entityEntry.State = EntityState.Detached;
                    }
                    catch (Exception ex)
                    {
                        Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                    }
                }
            }

            if (ChannelMessageEvent != null)
            {
                foreach (ChannelMessageDelegate chanDel in ChannelMessageEvent.GetInvocationList())
                {
                    Debug.Assert(Object.ReferenceEquals(chanDel.Target.GetType(), typeof(ChannelContext)), "Target of ChannelMessageDelegate not of type ChannelContext");
                    try
                    {
                        ChannelContext DelegateContext = (ChannelContext)chanDel.Target;
                        if (DelegateContext.ChannelName == ircChannel)
                        {
                            chanDel(tags, ircUser, ircChannel, message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Fatty.PrintWarningToScreen($"Falled to issue Channel Message events for {(curContext != null ? $"{curContext.ChannelName}" : "Error")}", ex.StackTrace);
                    }
                }
            }
        }

        public void HandleUserJoinChannel(string ircUser, string ircChannel, JoinType type)
        {
            lock (LoggingLock)
            {
                IrcLogUser FoundUser = Logging.Users.Find(ircUser, ServerLogInstance.Id);
                if (FoundUser == null)
                {
                    FoundUser = new IrcLogUser(ircUser, ServerLogInstance.Id, DateTime.Now);
                    Logging.Users.Add(FoundUser);
                    Logging.SaveChanges();
                }
                else
                {
                    FoundUser.LastSeen = DateTime.Now;
                    Logging.SaveChanges();
                }
            }
            if (UserJoinedEvent != null)
            {
                foreach (UserJoinPartDelegate joinDel in UserJoinedEvent.GetInvocationList())
                {
                    Debug.Assert(Object.ReferenceEquals(joinDel.Target.GetType(), typeof(ChannelContext)), "Target of ChannelMessageDelegate not of type ChannelContext");

                    ChannelContext DelegateContext = (ChannelContext)joinDel.Target;

                    if (DelegateContext.ChannelName == ircChannel)
                    {
                        joinDel(ircUser, ircChannel, type);
                    }
                }
            }
        }

        public void HandleUserstate(UserStateType type, Dictionary<string, string>? tags, string channel, string userName)
        {
            if (UserstateEvent != null)
            {
                foreach (UserstateDelegate userstateDelegate in UserstateEvent.GetInvocationList())
                {
                    ChannelContext delegateContext = (ChannelContext)userstateDelegate.Target;
                    if (delegateContext.ChannelName == channel)
                    {
                        userstateDelegate(type, tags, channel, userName);
                    }
                }
            }
        }

        public void HandleUserNotice(Dictionary<string, string>? tags, string channel, string userName)
        {
            if (UserNoticeEvent != null)
            {
                foreach (UserNoticeDelegate userNoticeDelegate in UserNoticeEvent.GetInvocationList())
                {
                    ChannelContext delegateContext = (ChannelContext)userNoticeDelegate.Target;
                    if (delegateContext.ChannelName == channel)
                    {
                        userNoticeDelegate(tags, channel, userName);
                    }
                }
            }
        }

        public void SendMessage(string ircChannel, string message)
        {
            OwnerConnection.SendMessage(ircChannel, message);
        }

        public string[] GetConnectedChannelUsers(ChannelContext ircChannel)
        {
            try
            {
                var result = OwnerConnection.GetChannelUsers(ircChannel);
                return result;
            }
            catch (Exception ex)
            {
                Fatty.PrintWarningToScreen(ex);
            }

            return Array.Empty<string>();
        }

        public void SendCapMessage(string cap)
        {
            OwnerConnection.SendCapRequest(cap);
        }

        public bool IsAuthenticatedUser(string UserToken)
        {
            foreach (string authMask in AuthenticatedMasks)
            {
                if (UserToken.Substring(UserToken.IndexOf("@") + 1) == authMask)
                {
                    return true;
                }
            }

            return false;
        }
        private void InitLogging()
        {
            lock (LoggingLock)
            {
                Fatty.PrintToScreen("Init Logging...");
                var LoggingFactory = new BloggingContextFactory();
                Logging = LoggingFactory.CreateDbContext(null);
                Fatty.PrintToScreen("Ensuring DB is present...");
                if (Logging.Database.EnsureCreated())
                {
                    Fatty.PrintToScreen("Database needed to be created");
                }


                IEnumerable<ServerLog> ServerQuery = Logging.Servers.AsQueryable().Where(x => x.ServerName == ServerName);
                if (ServerQuery.Count() == 0)
                {
                    ServerLog thisServer = new ServerLog(ServerName);
                    Logging.Servers.Add(thisServer);
                    Logging.SaveChanges();
                    ServerLogInstance = thisServer;
                }
                else
                {
                    ServerLogInstance = ServerQuery.First();
                }
            }
        }

#nullable enable
        public IrcLogUser? GetLoggedUserInfo(string ircUser)
        {

            IrcLogUser? foundUser = null;
            try
            {
                lock (LoggingLock)
                {
                    foundUser = Logging.Users.Find(ircUser, ServerLogInstance.Id);
                    if (foundUser != null)
                    {
                        // we don't want our recipients to modify this and accidentally commit it back to the DB.
                        var entry = Logging.Entry(foundUser);
                        entry.State = EntityState.Detached;
                    }
                }

            }
            catch (Exception ex)
            {
                Fatty.PrintWarningToScreen(ex.Message, ex.StackTrace);
                foundUser = null;
            }

            return foundUser;
        }
#nullable disable

    }
}
