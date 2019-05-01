using AgileServiceBus.Utilities;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AgileServiceBus.Test.Unit
{
    public class MessageGroupQueueTest
    {
        [Fact]
        public void GroupCreation()
        {
            MessageGroupQueue queue = new MessageGroupQueue(100);

            queue.AddGroup("groupid");
            Assert.Throws<ArgumentException>(() => queue.AddGroup("groupid"));
        }

        [Fact]
        public void GroupRemoval()
        {
            MessageGroupQueue queue = new MessageGroupQueue(100);
            queue.AddGroup("groupid");

            queue.RemoveGroup("groupid");
            queue.RemoveGroup("groupid");
        }

        [Fact]
        public void MessageEnqueuing()
        {
            MessageGroupQueue queue = new MessageGroupQueue(100);
            queue.AddGroup("groupid1");

            queue.AddMessage("message1", "groupid1");
            queue.AddMessage("message2", "groupid1");
            queue.AddMessage("message3", "groupid2");
        }

        [Fact]
        public void MessageWaiting()
        {
            MessageGroupQueue queue = new MessageGroupQueue(100);
            queue.AddGroup("groupid");

            string message = null;
            Task.Run(() =>
            {
                message = queue.WaitMessage("groupid");
            });

            queue.AddMessage("message", "groupid");

            Thread.Sleep(100);
            Assert.Equal("message", message);
        }

        [Fact]
        public void WaitingInterruption()
        {
            MessageGroupQueue queue = new MessageGroupQueue(100);
            queue.AddGroup("groupid");

            string message = "message";
            Stopwatch stopwatch = new Stopwatch();
            Task.Run(() =>
            {
                stopwatch.Start();
                message = queue.WaitMessage("groupid");
                stopwatch.Stop();
            });

            queue.Dispose();

            Thread.Sleep(100);
            Assert.Null(message);
            Assert.True(stopwatch.ElapsedMilliseconds < 100);
        }
    }
}