using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class FattyModule
    {
        protected ChannelContext OwningChannel;

        // called upon joining a server, useful for setting up callbacks
        public virtual void ChannelInit(ChannelContext channel)
        {
            OwningChannel = channel;
        }

        // called while fatty is starting, before connecting to any servers
        // ONLY MODIFY STATIC DATA FROM HERE
        public virtual void ModuleInit()
        {
            // STATIC STUFF ONLY
        }

        public virtual void RegisterEvents()
        {
        }
    }
}
