using System;
using System.Runtime.Serialization;

namespace Fatty
{
    [DataContract]
    class EmailConfig
    {
        [DataMember(IsRequired = true)]
        string SMTPAddress;

        [DataMember(IsRequired = true)]
        Int16 SMTPPort;

        [DataMember(IsRequired = true)]
        string EmailAddress;

        [DataMember(IsRequired = true)]
        string Password;
    }
}
