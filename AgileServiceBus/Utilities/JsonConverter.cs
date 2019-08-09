using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Globalization;

namespace AgileServiceBus.Utilities
{
    public class JsonConverter
    {
        public static JsonSerializerSettings Settings { get; private set; }

        static JsonConverter()
        {
            Settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore
            };

            Settings.Converters.Add(new StringEnumConverter()
            {
                AllowIntegerValues = false,
                NamingStrategy = new CamelCaseNamingStrategy()
            });
        }

        public string Serialize<TMessage>(TMessage message) where TMessage : class
        {
            return JsonConvert.SerializeObject(message, Settings);
        }

        public TMessage Deserialize<TMessage>(string message) where TMessage : class
        {
            return JsonConvert.DeserializeObject<TMessage>(message, Settings);
        }
    }
}