using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Fatty
{
    class FattyHelpers
    {
        public static T DeserializeFromPath<T>(string path)
        {
            try
            {
                StreamReader sr = new StreamReader(path);
                string objectString = sr.ReadToEnd();
                sr.Close();

                JsonValue objectValue = JsonValue.Parse(objectString);

                string valueString = objectValue.ToString();

                MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(valueString));

                var serializer = new DataContractJsonSerializer(typeof(T));
                T returnVal;
                returnVal = (T)serializer.ReadObject(ms);

                return returnVal;
            }
            catch (Exception e)
            {
                Fatty.PrintWarningToScreen($"Failed to deserialize \"{typeof(T).FullName}\" from path: {path} - {e.Message}", e.StackTrace);
                return default(T);
            }    
        }

        // pass null to get default serializer
        public static T DeserializeFromJsonString<T>(string json, DataContractJsonSerializerSettings SerializerSettings = null)
        {
            try
            {
                MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));

                if(SerializerSettings == null)
                {
                    SerializerSettings = new DataContractJsonSerializerSettings();
                }

                var serializer = new DataContractJsonSerializer(typeof(T), SerializerSettings);
                
                T returnVal;
                returnVal = (T)serializer.ReadObject(ms);

                return returnVal;
            }
            catch (Exception e)
            {
                Fatty.PrintToScreen($"Failed to deserialize object of type \"{typeof(T).FullName}\" from string - Exception: {e.Message}", ConsoleColor.Yellow);
                Fatty.PrintToScreen(json, ConsoleColor.Red);
                Fatty.PrintToScreen(e.StackTrace, ConsoleColor.Yellow);
                return default(T);
            }
        }

        public static string JsonSerializeFromObject<T>(T TargetObject, DataContractJsonSerializerSettings SerializerSettings = null)
        {
            try
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    if (SerializerSettings == null)
                    {
                        SerializerSettings = new DataContractJsonSerializerSettings();
                    }

                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T), SerializerSettings);
                    ser.WriteObject(memStream, TargetObject);


                    StreamReader sr = new StreamReader(memStream);
                    memStream.Position = 0;

                    return sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Fatty.PrintWarningToScreen($"Failed to serialize object of type :{typeof(T).FullName} - Exception: {e.Message}", e.StackTrace);
                return default(string);
            }
        }

        public static string RemoveCommandName(string message)
        {
            int firstSpace = message.IndexOf(" ");
            if (message.Length > firstSpace)
            {
                return message.Substring(message.IndexOf(" ") + 1);
            }
            else
            {
                return "";
            }
        }

        // todo: implement
        public static int GetMessageOverhead(string source, string messageType = "PRIVMSG", bool bIsTwitchServer = false)
        {
            // twitch servers have a 500 character limitation opposed to the regular 512, so we add 12 to overhead to account for that
            return source.Length + messageType.Length + (bIsTwitchServer ? 12 : 0);
        }

        public static bool StringContainsMulti(string input, string[] searchStrings, StringComparison compareType = StringComparison.Ordinal)
        {
            return Array.Exists(searchStrings, element => element.Equals(input, compareType));
        }

        public static List<Type> GetAllDerivedClasses<TBase>()
        {
            // Get the assembly containing the base class
            Assembly assembly = Assembly.GetAssembly(typeof(TBase));
            List<Type> derivedClasses = new List<Type>();

            // Iterate through all types in the assembly
            foreach (Type type in assembly.GetTypes())
            {
                // Check if the type is a class, not abstract, and a subclass of the base class
                if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(TBase)))
                {
                    derivedClasses.Add(type);
                }
            }

            return derivedClasses;
        }

    }
}
