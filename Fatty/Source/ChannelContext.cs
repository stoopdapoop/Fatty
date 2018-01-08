using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    [DataContract]
    class ChannelContext
    {
        [DataMember(IsRequired = true)]
        public string ChannelName { get; set; }

        [DataMember]
        public bool UseDefaultFeatures { get; set; }


    }
}
