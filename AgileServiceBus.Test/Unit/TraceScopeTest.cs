using AgileServiceBus.Tracing;
using FakeItEasy;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class TraceScopeTest
    {
        [Fact]
        public async Task RootTraceSpanCreation()
        {
            //tracer faking
            Tracer tracer = A.Fake<Tracer>();

            A.CallTo(() => tracer.CreateTraceId())
                .Returns("trace_id");

            A.CallTo(() => tracer.CreateSpanId())
                .Returns("span_id");

            string spanId = null;
            string parentSpanId = null;
            string traceId = null;
            DateTime startTime = DateTime.MinValue;
            DateTime endTime = DateTime.MinValue;
            string displayName = null;
            Dictionary<string, string> attributes = null;
            A.CallTo(() => tracer.SendAsync(A<TraceSpan>.Ignored))
                .Invokes((TraceSpan traceSpan) =>
                {
                    spanId = traceSpan.Id;
                    parentSpanId = traceSpan.ParentId;
                    traceId = traceSpan.TraceId;
                    startTime = traceSpan.StartTime;
                    endTime = traceSpan.EndTime;
                    displayName = traceSpan.DisplayName;
                    attributes = traceSpan.Attributes;
                });

            //span creating
            TraceScope traceScope = new TraceScope("root", tracer);
            traceScope.Attributes.Add("key", "value");
            await Task.Delay(1);
            traceScope.Dispose();

            //checking
            await Task.Delay(100);
            Assert.Equal("span_id", spanId);
            Assert.Null(parentSpanId);
            Assert.Equal("trace_id", traceId);
            Assert.NotEqual(startTime, DateTime.MinValue);
            Assert.NotEqual(endTime, DateTime.MinValue);
            Assert.True(endTime > startTime);
            Assert.Equal("root", displayName);
            Assert.NotNull(attributes);
            Assert.Single(attributes);
        }

        [Fact]
        public async Task ChildTraceSpanCreation()
        {
            //tracer faking
            Tracer tracer = A.Fake<Tracer>();

            A.CallTo(() => tracer.CreateSpanId())
                .Returns("span_id");

            string spanId = null;
            string parentSpanId = null;
            string traceId = null;
            DateTime startTime = DateTime.MinValue;
            DateTime endTime = DateTime.MinValue;
            string displayName = null;
            Dictionary<string, string> attributes = null;
            A.CallTo(() => tracer.SendAsync(A<TraceSpan>.Ignored))
                .Invokes((TraceSpan traceSpan) =>
                {
                    spanId = traceSpan.Id;
                    parentSpanId = traceSpan.ParentId;
                    traceId = traceSpan.TraceId;
                    startTime = traceSpan.StartTime;
                    endTime = traceSpan.EndTime;
                    displayName = traceSpan.DisplayName;
                    attributes = traceSpan.Attributes;
                });

            //span creating
            TraceScope traceScope = new TraceScope("parent_span_id", "trace_id", "child", tracer);
            traceScope.Attributes.Add("key", "value");
            await Task.Delay(1);
            traceScope.Dispose();

            //checking
            await Task.Delay(100);
            Assert.Equal("span_id", spanId);
            Assert.Equal("parent_span_id", parentSpanId);
            Assert.Equal("trace_id", traceId);
            Assert.NotEqual(startTime, DateTime.MinValue);
            Assert.NotEqual(endTime, DateTime.MinValue);
            Assert.True(endTime > startTime);
            Assert.Equal("child", displayName);
            Assert.NotNull(attributes);
            Assert.Single(attributes);
        }
    }
}