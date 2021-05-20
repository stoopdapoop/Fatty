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
                Fatty.PrintToScreen("Invalid Connections Config: " + e.Message);
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
                Fatty.PrintToScreen(e.Message, ConsoleColor.Yellow);
                return default(T);
            }
        }
    }
}
