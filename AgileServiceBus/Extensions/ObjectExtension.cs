using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Globalization;

namespace AgileSB.Extensions
{
    public static class ObjectExtension
    {
        public static string Serialize(this Object obj)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.Converters.Add(new StringEnumConverter() { NamingStrategy = new CamelCaseNamingStrategy(), AllowIntegerValues = false });
            settings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            settings.Culture = CultureInfo.InvariantCulture;
            settings.NullValueHandling = NullValueHandling.Ignore;

            string result = JsonConvert.SerializeObject(obj, settings);

            return (result);
        }
    }
}