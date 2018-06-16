namespace Fatty
{
    public class TalkBackModule : FattyModule
    {
        public TalkBackModule()
        {
            IsDefaultModule = true;
        }

        public override void RegisterEvents(IRCConnection connection)
        {
            connection.ChannelMessageEvent += OnChannelMessage;
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
