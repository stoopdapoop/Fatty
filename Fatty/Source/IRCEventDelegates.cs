namespace Fatty
{
    public delegate void ChannelMessageDelegate(string ircUser, string ircChannel, string message);
    public delegate void PrivateMessageDelegate(string ircUser, string message);
    public delegate void TopicSetDelgate(string ircChannel, string ircTopic);
    public delegate void TopicOwnerMessageDelegate(string ircChannel, string ircUser, string topicDate);
    public delegate void NamesListMessageDelegate(string userNames);
    public delegate void ServerMessageDelegate(string serverMessage);
    public delegate void JoinMessageDelegate(string ircChannel, string ircUser);
    public delegate void PartMessageDelegate(string ircChannel, string ircUser);
    public delegate void ModeMessageDelegate(string ircChannel, string ircUser, string userMode);
    public delegate void NickChangeMessageDelegate(string UserOldNick, string UserNewNick);
    public delegate void KickMessageDelegate(string ircChannel, string userKicker, string userKicked, string kickMessage);
    public delegate void QuitMessageDelegate(string userQuit, string quitMessage);
    public delegate void NoticeDelegate(string ircUser, string message);
    public delegate void ServerWelcome(int messageID);
}
