using AgileServiceBus.Utilities;
using System;
using System.Threading.Tasks;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class RetryHandlerTest
    {
        [Fact]
        public void ExceptionInclusion()
        {
            RetryHandler retryHandler = new RetryHandler(5, 15, 7, true);

            retryHandler
                .IncludeForRetry<TimeoutException>()
                .IncludeForRetry<NullReferenceException>();

            Assert.True(retryHandler.IsForRetry(new TimeoutException()));
            Assert.True(retryHandler.IsForRetry(new NullReferenceException()));
            Assert.False(retryHandler.IsForRetry(new ArgumentNullException()));
        }

        [Fact]
        public void ExceptionExclusion()
        {
            RetryHandler retryHandler = new RetryHandler(5, 15, 7, false);

            retryHandler
                .ExcludeForRetry<TimeoutException>()
                .ExcludeForRetry<NullReferenceException>();

            Assert.False(retryHandler.IsForRetry(new TimeoutException()));
            Assert.False(retryHandler.IsForRetry(new NullReferenceException()));
            Assert.True(retryHandler.IsForRetry(new ArgumentNullException()));
        }

        [Fact]
        public async Task ExecutionWithRetry()
        {
            RetryHandler retryHandler = new RetryHandler(5, 15, 7, true);

            retryHandler
                .IncludeForRetry<NullReferenceException>();

            short retryCount = -1;
            short errorCount = 0;

            await retryHandler.ExecuteAsync(async () =>
            {
                retryCount++;

                await Task.Delay(20);

                throw new NullReferenceException();
            },
            async (exception, retryIndex, retryLimit) =>
            {
                errorCount++;

                await Task.Delay(10);

                Assert.NotNull(exception);
                Assert.Equal((ushort)retryCount, retryIndex);
                Assert.Equal(7, retryLimit);
            });

            Assert.Equal(7, retryCount);
            Assert.Equal(8, errorCount);
        }

        [Fact]
        public async Task ExecutionWithoutRetry()
        {
            RetryHandler retryHandler = new RetryHandler(5, 15, 7, false);

            retryHandler
                .ExcludeForRetry<NullReferenceException>();

            short retryCount = -1;
            short errorCount = 0;

            await retryHandler.ExecuteAsync(async () =>
            {
                retryCount++;

                await Task.Delay(20);

                throw new NullReferenceException();
            },
            async (exception, retryIndex, retryLimit) =>
            {
                errorCount++;

                await Task.Delay(10);

                Assert.NotNull(exception);
                Assert.Equal(0, retryIndex);
                Assert.Equal(0, retryLimit);
            });

            Assert.Equal(0, retryCount);
            Assert.Equal(1, errorCount);
        }
    }
}