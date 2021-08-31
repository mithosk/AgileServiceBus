using System;
using System.Collections;
using System.IO;

namespace AgileServiceBus.Additionals
{
    public class Env
    {
        private static Hashtable _couples;

        static Env()
        {
            _couples = new Hashtable();

            string[] lines = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "\\env");

            foreach (string line in lines)
                if (!line.Trim().StartsWith("#") && line.Contains(">") && line.Replace(" ", "").Length >= 3)
                {
                    string key = line.Split('>')[0].Trim();
                    string value = line.Substring(line.Split('>')[0].Length + 1).Trim();

                    _couples.Add(key, value);
                }
        }

        public static string Get(string key)
        {
            return (string)_couples[key];
        }
    }
}