using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class FattyModule
    {
        [DataMember]
        public bool IsDefaultModule { get; protected set; }

        public virtual void RegisterEvents(IRCConnection connection)
        {

        }
    }
}
