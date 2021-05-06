using System;
using System.Collections.Generic;
using System.Text;

namespace Fatty
{
    public class UserCommand
    {
     
        public string CommandName;

        public CommandDelegate CommandCallback;

        public string CommandHelp;

        public UserCommand(string Name, CommandDelegate Callback, string Helptext)
        {
            CommandName = Name;
            CommandCallback = Callback;
            CommandHelp = Helptext;
        }
    }
}
