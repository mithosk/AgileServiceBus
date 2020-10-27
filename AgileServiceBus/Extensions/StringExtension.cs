using AgileServiceBus.Exceptions;
using NCrontab;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AgileServiceBus.Extensions
{
    public static class StringExtension
    {
        public static Dictionary<string, string> ParseConnStr(this string str)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();

            string[] couples = str.Split(';');
            foreach (string couple in couples)
            {
                string key = couple.Split('=')[0];
                string value = couple.Split('=')[1];
                settings.Add(key, value);
            }

            return settings;
        }

        public static void CheckNaming(this string str)
        {
            string exceptionMessage = str + " is a invalid name";

            Regex regex = new Regex("^[a-zA-Z0-9]+$");
            if (!regex.IsMatch(str))
                throw new NamingException(exceptionMessage);

            switch (str.ToLower())
            {
                case "request":
                case "response":
                case "event":
                case "dlq":
                    throw new NamingException(exceptionMessage);
            }
        }

        public static async Task CronDelay(this string str)
        {
            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(str);
            DateTime nextDate = crontabSchedule.GetNextOccurrence(DateTime.UtcNow);
            TimeSpan delay = nextDate - DateTime.UtcNow;

            await Task.Delay(delay);
        }
    }
}