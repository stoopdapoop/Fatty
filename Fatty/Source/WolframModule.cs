﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Net;
using System.IO;
using System.Web;
using System.Runtime.Serialization;

namespace Fatty
{
    class WolframModule : FattyModule
    {
        [DataContract]
        private class WolframConfig
        {
            [DataMember(IsRequired = true)]
            public string WolframAlphaKey { get; private set; }
            [DataMember(IsRequired = true)]
            public int MaxCallsPerHour { get; private set; }
        }


        WolframConfig Config;
        private List<DateTime> RecentMathInvocations = new List<DateTime>();

        public WolframModule()
        {
        }

        public override void PostConnectionModuleInit()
        {
            base.PostConnectionModuleInit();

            if (Config == null)
            {
                Config = FattyHelpers.DeserializeFromPath<WolframConfig>("WolframAlpha.cfg");
            }
        }


        public override void ChannelInit(ChannelContext channel)
        {
            base.ChannelInit(channel);
        }


        public override void GetAvailableCommands(ref List<UserCommand> Commands)
        {
            Commands.Add(new UserCommand("c", ComputeCommand, "General Calculations"));
            Commands.Add(new UserCommand("calc", ComputeCommand, "General Calculations"));
            Commands.Add(new UserCommand("calclimit", CalcLimitCommand, "Check Calc API Rate limit"));
        }

        public override void ListCommands(ref List<string> CommandNames)
        {
            CommandNames.Add("c");
            CommandNames.Add("calc");
            CommandNames.Add("calclimit");
        }

        private void NoelCommand(string ircUser, string ircChannel, string message)
        {
            OwningChannel.SendMessage("NO!", ircUser);
        }
        public void CalcLimitCommand(string ircUser, string ircChannel, string message)
        {
            //cull old messages
            TimeSpan anHour = new TimeSpan(1, 0, 0);
            for (int i = RecentMathInvocations.Count - 1; i >= 0; --i)
            {
                if ((DateTime.Now - RecentMathInvocations[i]) > anHour)
                    RecentMathInvocations.RemoveAt(i);
            }
            OwningChannel.SendChannelMessage($"{RecentMathInvocations.Count} wolfram invocations have been made in the past hour. {30 - RecentMathInvocations.Count} left.");
        }

        public void ComputeCommand(string ircUser, string ircChannel, string message)
        {

            //cull old messages
            TimeSpan anHour = new TimeSpan(1, 0, 0);
            for (int i = RecentMathInvocations.Count - 1; i >= 0; --i)
            {
                if ((DateTime.Now - RecentMathInvocations[i]) > anHour)
                    RecentMathInvocations.RemoveAt(i);
            }

            if (RecentMathInvocations.Count > Config.MaxCallsPerHour)
            {
                TimeSpan nextInvoke = anHour - (DateTime.Now - RecentMathInvocations[0]);
                OwningChannel.SendChannelMessage($"Sorry {ircUser}, rate limit on this command exceeded, you can use it again in {nextInvoke.Minutes} minutes");
                return;
            }

            message = message.Substring(message.IndexOf(" ") + 1);
            string args = HttpUtility.UrlEncode(message);
            string searchURL = $"http://api.wolframalpha.com/v2/query?input={args}&appid={Config.WolframAlphaKey}";
            HttpWebRequest searchRequest = HttpWebRequest.Create(searchURL) as HttpWebRequest;
            HttpWebResponse searchResponse = searchRequest.GetResponse() as HttpWebResponse;
            StreamReader reader = new StreamReader(searchResponse.GetResponseStream());

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(reader);



            StringBuilder messageAccumulator = new StringBuilder();
            //int messageOverhead = FattyBot.GetMessageOverhead(info.Source);
            Fatty.PrintToScreen(reader.ReadToEnd());

            // todo
            int messageOverhead = 20;

            XmlNodeList res = xmlDoc.GetElementsByTagName("queryresult");
            if (res[0].Attributes["success"].Value == "false")
            {
                messageAccumulator.Append("Query failed: ");
                res = xmlDoc.GetElementsByTagName("tip");
                for (int i = 0; i < res.Count; i++)
                {
                    string desc = res[i].Attributes["text"].Value;
                    if (desc.Length + messageOverhead + messageAccumulator.Length <= 480)
                        messageAccumulator.Append(desc + ". ");
                    else
                        break;
                }
                res = xmlDoc.GetElementsByTagName("didyoumean");

                for (int i = 0; i < res.Count; i++)
                {
                    string desc = "";
                    if (i == 0)
                        desc += "Did you mean: ";
                    desc += res[i].InnerText + "? ";
                    if (desc.Length + messageOverhead + messageAccumulator.Length <= 480)
                        messageAccumulator.Append(desc);
                    else
                        break;
                }
                OwningChannel.SendChannelMessage(messageAccumulator.ToString());
            }
            else
            {
                res = xmlDoc.GetElementsByTagName("plaintext");

                for (int i = 0; i < res.Count; i++)
                {
                    string value = res[i].InnerText;
                    string description = res[i].ParentNode.ParentNode.Attributes["title"].Value;
                    if (description == "Number line")
                        continue;
                    description = description + ":" + value;
                    if (description.Length + messageOverhead + messageAccumulator.Length <= 480)
                        messageAccumulator.Append(description + " || ");
                    else
                        break;
                }
                if (messageAccumulator.Length > 0)
                {
                    messageAccumulator.Remove(messageAccumulator.Length - 1, 1);
                    messageAccumulator.Replace("\n", " ");
                    messageAccumulator.Replace("\r", " ");
                    OwningChannel.SendChannelMessage(messageAccumulator.ToString());
                }
                else
                {
                    OwningChannel.SendChannelMessage($"The result was likely too big to fit into a single message. Sorry {ircUser} :[");
                }
            }

            RecentMathInvocations.Add(DateTime.Now);
        }
    }
}