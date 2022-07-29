using System;
using System.IO;
using System.Json;
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
        public static int GetMessageOverhead(string source)
        {
            return 20;
        }

    }
}
