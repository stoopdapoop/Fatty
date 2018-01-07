using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    partial class IRCConnection
    {
        private class WelcomeProgress
        {
            public delegate void WelcomeComplete();
            public event WelcomeComplete WelcomeCompleteEvent;

            // bitfield to keep track of which messages have been received from the server
            private byte MessagesReceived;
            private bool HasFinishedWelcome;

            public void NotifyOfMessage(byte messageID)
            {
                if (messageID < 1 || messageID > 4)
                {
                    Console.WriteLine("Received invalid welcome message ID: {0}", messageID);
                }
                else
                {
                    byte testBit = (byte)(1 << messageID);
                    MessagesReceived = (byte)(MessagesReceived | testBit);

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

            public bool IsWelcomeComplete()
            {
                return (MessagesReceived == (MessagesReceived & (byte)14));
            }
        }
    }
}
