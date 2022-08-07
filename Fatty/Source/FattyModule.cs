using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public abstract class FattyModule
    {
        protected ChannelContext OwningChannel;

        // called before joining a channel or server, useful for setting up callbacks. called serially from main thread
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

        public abstract void RegisterAvailableCommands(ref List<UserCommand> Commands);
    }
}
