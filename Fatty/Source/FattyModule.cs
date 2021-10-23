using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public abstract class FattyModule
    {
        protected ChannelContext OwningChannel;

        // called upon joining a channel, useful for setting up callbacks
        public virtual void ChannelInit(ChannelContext channel)
        {
            OwningChannel = channel;
        }

        // called upon joining a server, useful for stateful data that needs to persist between channels
        public virtual void PostConnectionModuleInit()
        {

        }

        public virtual void RegisterEvents()
        {
        }

        // todo: remove this
        public abstract void ListCommands(ref List<string> CommandNames);

        public abstract void GetAvailableCommands(ref List<UserCommand> Commands);
    }
}
