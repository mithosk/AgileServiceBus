using AgileServiceBus.Utilities;
using System;
using System.Collections.Generic;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class JsonConverterTest
    {
        [Fact]
        public void JsonSerialization()
        {
            List<Car> cars = new List<Car>
            {
                new Car
                {
                    Brand = "Alfa Romeo",
                    Seats = 5,
                    Color = Color.Red,
                    RegistrationDate = null,
                    Price = null
                },
                new Car
                {
                    Brand = null,
                    Seats = null,
                    Color = null,
                    RegistrationDate = DateTime.Parse("2010-07-24 08:22:33"),
                    Price = 19004.67f
                },
                new Car
                {
                    Brand = "Mercedes-Benz",
                    Seats = null,
                    Color = Color.LightBlue,
                    RegistrationDate = null,
                    Price = null
                }
            };

            JsonConverter jsonConverter = new JsonConverter();
            string json = jsonConverter.Serialize(cars);

            Assert.Equal("[{\"brand\":\"Alfa Romeo\",\"seats\":5,\"color\":\"red\"},{\"registrationDate\":\"2010-07-24T08:22:33\",\"price\":19004.67},{\"brand\":\"Mercedes-Benz\",\"color\":\"lightBlue\"}]", json);
        }

        private class Car
        {
            public string Brand { get; set; }
            public byte? Seats { get; set; }
            public Color? Color { get; set; }
            public DateTime? RegistrationDate { get; set; }
            public float? Price { get; set; }
        }

        private enum Color
        {
            Red = 0,
            LightBlue = 1
        }
    }
}