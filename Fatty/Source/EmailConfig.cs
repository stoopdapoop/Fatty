using System;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    class EmailConfig
    {
        [DataMember(IsRequired = true)]
        public string SMTPAddress;

        [DataMember(IsRequired = true)]
        public Int16 SMTPPort;

        [DataMember(IsRequired = true)]
        public string EmailAddress;

        [DataMember(IsRequired = true)]
        public string Password;
    }
}
