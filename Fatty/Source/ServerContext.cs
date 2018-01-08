using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Fatty
{
    [DataContract]
    public class ServerContext
    {
        [DataMember(IsRequired = true)]
        public string ServerURL { get; set; }

        [DataMember(IsRequired = true)]
        public UInt16 ServerPort { get; set; }

        [DataMember(IsRequired = true)]
        public string Nick { get; set; }

        [DataMember(IsRequired = true)]
        public string RealName { get; set; } = "FattyBot";

        [DataMember(IsRequired = true)]
        public bool ShouldPrintToScreen { get; set; }

        [DataMember]
        public string AuthPassword { get; set; }

        [DataMember]
        public List<ChannelContext> Channels { get; set; }

        [DataMember]
        public string QuitMessage { get; set; }

        
    }
}
