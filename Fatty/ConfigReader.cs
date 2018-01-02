using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Fatty {
    class ConfigReader {

        private Dictionary<string, string> Defines;
        private Dictionary<string, string[]> DefineArrays;

        public ConfigReader() {
            Defines = new Dictionary<string, string>();
            DefineArrays = new Dictionary<string, string[]>();
        }

        public bool AddConfig(string filePath){
            try
            {
                StreamReader sr = new StreamReader(filePath);

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    // ignore comments that start with exclamation points
                    if (line[0] == '!' || line.Length == 0)
                        continue;

                    /* array elements are of format:
                     <name=value1
                     value2
                     >  */
                    if (line[0] == '<')
                    {
                        int assignPos = line.IndexOf("=");
                        string defineKey = line.Substring(1, assignPos - 1);

                        List<string> defineValue = new List<string>();
                        defineValue.Add(line.Substring(assignPos + 1, line.Length - (assignPos + 1)));
                        while ((line = sr.ReadLine()) != null && line != ">")
                        {
                            defineValue.Add(line);
                        }
                        DefineArrays.Add(defineKey, defineValue.ToArray());
                    }
                    // everything else is of format <name>=<value>
                    else
                    {
                        int assignPos = line.IndexOf("=");
                        string defineKey = line.Substring(0, assignPos);
                        string defineValue = line.Substring(assignPos + 1, line.Length - (assignPos + 1));
                        Defines.Add(defineKey, defineValue);
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public string GetValue(string key) {
            string value;
            bool found = Defines.TryGetValue(key, out value);
            if (found)
                return value;
            else
                return "";
        }

        public string[] GetValueArray(string key) {
            string[] value;
            bool found = DefineArrays.TryGetValue(key, out value);
            if (found)
                return value;
            else
                return new string[]{""};
        }
    }
}
