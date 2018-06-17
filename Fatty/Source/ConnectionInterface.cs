namespace Fatty
{
    // Class that exposes events to modules
    public class ConnectionInterface
    {

        private IRCConnection OwningConnection;

        public ConnectionInterface(IRCConnection owner)
        {
            OwningConnection = owner;
        }

        public void AddChannelMessageCallback(ChannelMessageDelegate callback)
        {
            OwningConnection.ChannelMessageEvent += callback;
        }

        public void AddPrivateMessageCallback(PrivateMessageDelegate callback)
        {
            OwningConnection.PrivateMessageEvent += callback;
        }

        public void AddNoticeCallback(NoticeDelegate callback)
        {
            OwningConnection.NoticeEvent += callback;
        }
    }
}
