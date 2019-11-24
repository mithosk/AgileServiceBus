using System;
using System.Collections.Generic;

namespace AgileSB.Extensions
{
    public static class StringExtension
    {
        public static Dictionary<string, string> ParseAsConnectionString(this String str)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();

            string[] couples = str.Split(';');
            foreach (string couple in couples)
            {
                string key = couple.Split('=')[0];
                string value = couple.Split('=')[1];
                if (String.IsNullOrEmpty(value) == true)
                    value = null;

                settings.Add(key, value);
            }

            return settings;
        }
    }
}