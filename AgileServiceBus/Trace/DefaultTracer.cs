using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Trace
{
    public class DefaultTracer : Tracer
    {
        public async override Task TraceAsync(TraceSpan span)
        {
            string deep = span.ParentId == null ? "Root" : "Child";
            string time = ((int)((span.EndTime - span.StartTime).TotalMilliseconds + 0.5)).ToString();
            string displayName = span.DisplayName;

            await Console.Out.WriteLineAsync("Trace:    " + deep.PadLeft(5, ' ') + " [" + time.PadLeft(5, ' ') + "]  " + displayName);
        }
    }
}