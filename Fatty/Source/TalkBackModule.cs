namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        public TalkBackModule()
        {
            IsDefaultModule = true;
        }

        public override void RegisterEvents(ConnectionInterface connection)
        {
            connection.AddChannelMessageCallback(OnChannelMessage);
        }

        void OnChannelMessage(IRCConnection connection, string ircUser, string ircChannel, string message)
        {
            if(message.Contains(connection.Context.Nick))
            {
                connection.SendMessage(ircChannel, "forget u");
            }
        }
    }
}
