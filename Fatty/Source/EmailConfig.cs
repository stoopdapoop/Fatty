using System;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    class EmailConfig
    {
        [DataMember(IsRequired = true)]
        public string SMTPAddress { get; private set; }

        [DataMember(IsRequired = true)]
        public Int16 SMTPPort { get; private set; }

        [DataMember(IsRequired = true)]
        public string EmailAddress { get; private set; }

        [DataMember(IsRequired = true)]
        public string Password { get; private set; }
    }
}
