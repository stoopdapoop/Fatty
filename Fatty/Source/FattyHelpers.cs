using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Json;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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

        public static bool JsonSerializeToPath<T>(T TargetObject, string Path, DataContractJsonSerializerSettings SerializerSettings = null)
        {
            try
            {
                string content = JsonSerializeFromObject<T>(TargetObject, SerializerSettings);
                File.WriteAllText(Path, content);
            }
            catch (Exception e)
            {
                Fatty.PrintWarningToScreen($"Failed to serialize object of type :{typeof(T).FullName} - Exception: {e.Message}", e.StackTrace);
                return false;
            }

            return true;
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

        public static int GetMessageOverhead(string source, string messageType = "PRIVMSG", bool bIsTwitchServer = false)
        {
            // twitch servers have a 500 character limitation opposed to the regular 512, so we add 12 to overhead to account for that
            return source.Length + 1 + messageType.Length + (bIsTwitchServer ? 12 : 0);
        }

        public static bool StringContainsMulti(string input, string[] searchStrings, StringComparison compareType = StringComparison.Ordinal)
        {
            return Array.Exists(searchStrings, element => element.Equals(input, compareType));
        }

        public static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            return new string(normalizedString.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
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
        // Body can be string, Dictionary, or FormUrlEncodedContent
        public static async Task<HttpResponseMessage> HttpRequest(string BaseAddress, string Endpoint, HttpMethod method, NameValueCollection? URIQueries = null, HttpRequestHeaders? headers = null, object? Body = null)
        {
            UriBuilder uriBuilder = new UriBuilder(BaseAddress)
            {
                Path = Endpoint
            };

            if (URIQueries != null)
            {
                NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query.Add(URIQueries);
                uriBuilder.Query = query.ToString();
            }

            HttpRequestMessage request = new HttpRequestMessage(method, uriBuilder.Uri);

            HttpClient client = new HttpClient()
            {
                BaseAddress = new Uri(BaseAddress)
            };
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            HttpContent content = null;

            if (Body is Dictionary<string, string> bodyDict)
            {
                content = new FormUrlEncodedContent(bodyDict);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            }
            else if (Body is string bodyString)
            {
                content = new StringContent(bodyString);
            }

            request.Content = content;

            return await client.SendAsync(request);
        }

        public static HttpRequestHeaders CreateHTTPRequestHeaders()
        {
            return new HttpRequestMessage().Headers;
        }

    }
}
