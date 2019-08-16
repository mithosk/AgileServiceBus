using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Globalization;

namespace AgileServiceBus.Utilities
{
    public class JsonConverter
    {
        private JsonSerializerSettings _settings;

        public JsonConverter()
        {
            _settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore
            };

            _settings.Converters.Add(new StringEnumConverter()
            {
                AllowIntegerValues = false,
                NamingStrategy = new CamelCaseNamingStrategy()
            });
        }

        public string Serialize<TMessage>(TMessage message) where TMessage : class
        {
            return JsonConvert.SerializeObject(message, _settings);
        }

        public TMessage Deserialize<TMessage>(string message) where TMessage : class
        {
            return JsonConvert.DeserializeObject<TMessage>(message, _settings);
        }
    }
}