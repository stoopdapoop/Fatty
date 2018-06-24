using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // todo: make this read only
        [DataMember]
        public List<ChannelContext> Channels { get; private set; }

        [DataMember]
        public string QuitMessage { get; private set; }

        public event ChannelMessageDelegate ChannelMessageEvent;

        private IRCConnection OwnerConnection { get; set; }

        public void Initialize(IRCConnection irc)
        {
            OwnerConnection = irc;

            foreach(ChannelContext context in Channels)
            {
                context.Initialize(this);
            }
        }

        public void HandleServerMessage(string ircUser, string ircChannel, string message)
        {
            if (ChannelMessageEvent != null)
            {
                foreach (ChannelMessageDelegate chanDel in ChannelMessageEvent.GetInvocationList())
                {
                    Debug.Assert(Object.ReferenceEquals(chanDel.Target.GetType(), typeof(ChannelContext)), "Target of ChannelMessageDelegate not of type ChannelContext");

                    ChannelContext DelegateContext = (ChannelContext)chanDel.Target;
                    if (DelegateContext.ChannelName == ircChannel)
                    {
                        chanDel.BeginInvoke(ircUser, ircChannel, message, null, null);
                    }
                }
            }
        }

        public void SendMessage(string ircChannel, string message)
        {
            OwnerConnection.SendMessage(ircChannel, message);
        }

    }
}
