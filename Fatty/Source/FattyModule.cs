using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class FattyModule
    {
        protected ChannelContext OwningChannel;

        public virtual void Init(ChannelContext channel)
        {
            OwningChannel = channel;
        }

        public virtual void RegisterEvents()
        {
        }
    }
}
