using System;
using System.IO;
using System.Json;
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
                Console.WriteLine("Invalid Connections Config: " + e.Message);
                return default(T);
            }    
        }

        public static T DeserializeFromJsonString<T>(string json)
        {
            try
            {
                MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));

                var serializer = new DataContractJsonSerializer(typeof(T));
                T returnVal;
                returnVal = (T)serializer.ReadObject(ms);

                return returnVal;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return default(T);
            }
        }
    }
}
