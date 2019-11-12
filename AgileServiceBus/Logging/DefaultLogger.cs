using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Logging
{
    public class DefaultLogger : Logger
    {
        public async override Task SendAsync(MessageDetail messageDetail)
        {
            string type = messageDetail.Type.ToString();
            string status = messageDetail.Exception == null ? "OK" : "KO";
            string extendedName = messageDetail.Directory + "." + messageDetail.Subdirectory + "." + messageDetail.Name;

            await Console.Out.WriteLineAsync("Logger:" + (type + " [   " + status + "]").PadLeft(17, ' ') + "  " + extendedName);
        }
    }
}