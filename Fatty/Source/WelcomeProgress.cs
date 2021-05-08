using System;

namespace Fatty
{
    partial class IRCConnection
    {
        private class WelcomeProgress
        {
            public delegate void WelcomeComplete();
            public event WelcomeComplete WelcomeCompleteEvent;
            private Object WelcomeLock = new object();

            // bitfield to keep track of which messages have been received from the server
            private int MessagesReceived = 0;
            private bool HasFinishedWelcome;

            public void NotifyOfMessage(int messageID)
            {
                lock (WelcomeLock)
                {
                    if (messageID < 1 || messageID > 4)
                    {
                        Fatty.PrintToScreen("Received invalid welcome message ID: {0}", messageID);
                    }
                    else
                    {
                        MessagesReceived += messageID;

                        if (IsWelcomeComplete())
                        {
                            if (!HasFinishedWelcome)
                            {
                                WelcomeCompleteEvent();
                                HasFinishedWelcome = true;
                            }
                        }
                    }
                }
            }

            private bool IsWelcomeComplete()
            {
                return (MessagesReceived == 1 + 2 + 3 + 4);
            }
        }
    }
}
