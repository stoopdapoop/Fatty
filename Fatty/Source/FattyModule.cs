using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
