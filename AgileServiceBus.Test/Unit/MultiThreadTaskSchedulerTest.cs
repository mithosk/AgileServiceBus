﻿using AgileServiceBus.Utilities;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class MultiThreadTaskSchedulerTest
    {
        [Fact]
        public async Task TaskCreation()
        {
            MultiThreadTaskScheduler taskScheduler = new MultiThreadTaskScheduler(1);

            int threadId1 = 0;
            int threadId2 = 0;
            int threadId3 = 0;

            await await Task.Factory.StartNew(async () =>
            {
                threadId1 = Thread.CurrentThread.ManagedThreadId;
                await Task.Delay(10);
                threadId2 = Thread.CurrentThread.ManagedThreadId;
                await Task.Delay(10);
                threadId3 = Thread.CurrentThread.ManagedThreadId;
            },
            new CancellationToken(),
            TaskCreationOptions.DenyChildAttach,
            taskScheduler);

            Assert.Equal(threadId1, threadId2);
            Assert.Equal(threadId2, threadId3);
        }

        [Fact]
        public async Task TaskInterruption()
        {
            MultiThreadTaskScheduler taskScheduler = new MultiThreadTaskScheduler(14);

            int threadId = 0;

            await Task.Factory.StartNew(async () =>
            {
                await Task.Delay(100);
                threadId = Thread.CurrentThread.ManagedThreadId;
            },
            new CancellationToken(),
            TaskCreationOptions.DenyChildAttach,
            taskScheduler);

            await Task.Delay(10);
            taskScheduler.Dispose();

            Assert.Equal(0, threadId);
        }
    }
}