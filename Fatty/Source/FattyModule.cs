using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class FattyModule
    {
        [DataMember]
        public string ModuleName { get; }

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
