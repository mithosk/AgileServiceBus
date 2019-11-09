using AgileServiceBus.Logging;
using System;
using System.Threading.Tasks;

namespace AgileSB.Logging
{
    public class DefaultLogger : Logger
    {
        public async override Task SendAsync(MessageDetail messageDetail)
        {
            await Console.Out.WriteLineAsync(messageDetail.Name + ": " + messageDetail.Body);
        }
    }
}