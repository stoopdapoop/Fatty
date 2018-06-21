using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    public class FattyModule
    {
        [DataMember]
        public bool IsDefaultModule { get; protected set; }

        [DataMember]
        public string ModuleName { get; }

        public virtual void RegisterEvents()
        {
            // todo: fix meeeee
        }
    }
}
