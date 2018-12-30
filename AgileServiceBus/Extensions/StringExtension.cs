using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;

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

            return (settings);
        }

        public static T Deserialize<T>(this String str)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.Converters.Add(new StringEnumConverter() { NamingStrategy = new CamelCaseNamingStrategy(), AllowIntegerValues = false });
            settings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            settings.Culture = CultureInfo.InvariantCulture;
            settings.NullValueHandling = NullValueHandling.Ignore;

            T result = JsonConvert.DeserializeObject<T>(str, settings);

            return (result);
        }
    }
}