using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace AgileSB.Extensions
{
    public static class ObjectExtension
    {
        public static string Serialize(this Object obj)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.Converters.Add(new StringEnumConverter() { CamelCaseText = true, AllowIntegerValues = false });
            settings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            settings.Culture = CultureInfo.InvariantCulture;
            settings.NullValueHandling = NullValueHandling.Ignore;

            string result = JsonConvert.SerializeObject(obj, settings);

            return (result);
        }

        public static void Validate(this Object obj)
        {
            ValidationContext validationContext = new ValidationContext(obj, null, null);
            List<ValidationResult> validationResults = new List<ValidationResult>();
            bool valid = Validator.TryValidateObject(obj, validationContext, validationResults, true);

            if (valid == false)
            {
                ValidationException validationException = new ValidationException("Invalid Object data");
                foreach (ValidationResult validationResult in validationResults)
                    validationException.Data.Add(validationResult.MemberNames.First(), validationResult.ErrorMessage);

                throw (validationException);
            }
        }
    }
}