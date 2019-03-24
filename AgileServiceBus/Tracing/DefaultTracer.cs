using System;
using System.Threading.Tasks;

namespace AgileServiceBus.Tracing
{
    public class DefaultTracer : Tracer
    {
        public override string CreateTraceId()
        {
            return Guid.NewGuid().ToString();
        }

        public override string CreateSpanId()
        {
            return Guid.NewGuid().ToString();
        }

        public async override Task SendAsync(TraceSpan traceSpan)
        {
            string deep = traceSpan.ParentId == null ? "Root" : "Child";
            string time = ((int)((traceSpan.EndTime - traceSpan.StartTime).TotalMilliseconds + 0.5)).ToString();
            string displayName = traceSpan.DisplayName;

            await Console.Out.WriteLineAsync("Tracer:    " + deep.PadLeft(5, ' ') + " [" + time.PadLeft(5, ' ') + "]  " + displayName);
        }
    }
}