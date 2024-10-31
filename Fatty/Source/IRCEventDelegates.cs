using System.Collections.Generic;

namespace Fatty
{
    public enum JoinType
    {
        Join,
        Part,
        Invalid
    }

    public enum NoticeType
    {
        NOTICE,
        WHISPER
    }

    public enum UserStateType
    {
        Global,
        User,
        Room
    }
    public delegate void CommandDelegate(string ircUser, string ircChannel, string message);
    public delegate void PluginChannelMessageDelegate(Dictionary<string, string>? tags, string ircUser, string message);
    public delegate void UserJoinPartDelegate(string ircUser, string ircChannel, JoinType type);
    public delegate void PluginChannelJoinedDelegate(string ircChannel);
    public delegate void ChannelJoinedDelegate(string ircChannel);
    public delegate void ChannelMessageDelegate(Dictionary<string, string>? tags, string ircUser, string ircChannel, string message);
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
    public delegate void NoticeWhisperDelegate(NoticeType type, string ircUser, string message);
    public delegate void ServerWelcome(int messageID);
    public delegate void UserstateDelegate(UserStateType type, Dictionary<string, string>? tags, string channel, string username);
}
