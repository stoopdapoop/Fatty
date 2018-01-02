using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    class Program
    {
        static IRC Irc;
        static ConfigReader Config;

        static void Main(string[] args)
        {
            Config = new ConfigReader();
            Config.AddConfig("Connection.cfg");


        }
    }
}
