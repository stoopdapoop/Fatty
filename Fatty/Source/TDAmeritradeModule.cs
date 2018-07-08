using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    class TDAmeritradeModule : FattyModule
    {
        [DataContract]
        class TDAmeritradeConfig
        {
            [DataMember(IsRequired = true)]
            public string RefreshToken { get; private set; }

            [DataMember(IsRequired = true)]
            public string AccessToken { get; private set; }

            [DataMember(IsRequired = true)]
            public string ClientID { get; private set; }

            [DataMember(IsRequired = true)]
            public string RedirectURI { get; private set; }
        }

        TDAmeritradeConfig Config;

        public override void Init(ChannelContext channel)
        {
            base.Init(channel);

            Config = FattyHelpers.DeserializeFromPath<TDAmeritradeConfig>("TDAmeritrade.cfg");
            // get accounts
        }
    }
}
