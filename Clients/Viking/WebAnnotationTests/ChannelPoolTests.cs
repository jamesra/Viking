using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    [TestClass]
    public class ChannelPoolTests
    {
        private sealed class FakeChannel : ICommunicationObject
        {
            public CommunicationState State { get; set; } = CommunicationState.Created;
            public int Aborts { get; private set; }
            public int Closes { get; private set; }

            public void Open() => State = CommunicationState.Opened;
            public void Close() { Closes++; State = CommunicationState.Closed; }
            public void Abort() { Aborts++; State = CommunicationState.Closed; }

            public void Open(TimeSpan timeout) => Open();
            public void Close(TimeSpan timeout) => Close();
            public IAsyncResult BeginOpen(AsyncCallback callback, object state) => throw new NotSupportedException();
            public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state) => throw new NotSupportedException();
            public void EndOpen(IAsyncResult result) => throw new NotSupportedException();
            public IAsyncResult BeginClose(AsyncCallback callback, object state) => throw new NotSupportedException();
            public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state) => throw new NotSupportedException();
            public void EndClose(IAsyncResult result) => throw new NotSupportedException();

            public event EventHandler Closed { add { } remove { } }
            public event EventHandler Closing { add { } remove { } }
            public event EventHandler Faulted { add { } remove { } }
            public event EventHandler Opened { add { } remove { } }
            public event EventHandler Opening { add { } remove { } }
        }

        [TestMethod]
        public void TwoRentersNeverShareAChannel()
        {
            ChannelPool<FakeChannel> pool = new(() => new FakeChannel());
            FakeChannel first = pool.Rent();
            FakeChannel second = pool.Rent();
            Assert.AreNotSame(first, second);
            Assert.AreEqual(CommunicationState.Opened, first.State);
        }

        [TestMethod]
        public void AReturnedChannelIsLentAgainWithoutBeingClosed()
        {
            int created = 0;
            ChannelPool<FakeChannel> pool = new(() => { created++; return new FakeChannel(); });
            FakeChannel channel = pool.Rent();
            pool.Return(channel);

            Assert.AreSame(channel, pool.Rent());
            Assert.AreEqual(1, created);
            Assert.AreEqual(0, channel.Closes + channel.Aborts);
        }

        [TestMethod]
        public void ADiscardedChannelIsAbortedAndNeverLentAgain()
        {
            ChannelPool<FakeChannel> pool = new(() => new FakeChannel());
            FakeChannel channel = pool.Rent();
            ChannelPool<FakeChannel>.Discard(channel);
            pool.Return(channel);

            Assert.AreEqual(1, channel.Aborts);
            Assert.AreNotSame(channel, pool.Rent());
        }

        [TestMethod]
        public void AChannelThatFaultedWhileIdleIsSkipped()
        {
            ChannelPool<FakeChannel> pool = new(() => new FakeChannel());
            FakeChannel channel = pool.Rent();
            pool.Return(channel);
            channel.State = CommunicationState.Faulted;

            FakeChannel next = pool.Rent();
            Assert.AreNotSame(channel, next);
            Assert.AreEqual(1, channel.Aborts);
            Assert.AreEqual(0, pool.IdleCount);
        }

        [TestMethod]
        public void SurplusReturnedChannelsAreClosedNotKept()
        {
            ChannelPool<FakeChannel> pool = new(() => new FakeChannel(), maxIdle: 1);
            FakeChannel a = pool.Rent();
            FakeChannel b = pool.Rent();
            pool.Return(a);
            pool.Return(b);

            Assert.AreEqual(1, pool.IdleCount);
            Assert.AreEqual(1, b.Closes);
        }

        [TestMethod]
        public void AChannelThatFailsToOpenIsAbortedAndTheErrorPropagates()
        {
            ChannelPool<FailingOpen> pool = new(() => new FailingOpen());
            Assert.ThrowsExactly<CommunicationException>(() => pool.Rent());
            Assert.AreEqual(0, pool.IdleCount);
        }

        private sealed class FailingOpen : ICommunicationObject
        {
            public CommunicationState State => CommunicationState.Created;
            public void Open() => throw new CommunicationException("no route");
            public void Abort() { }
            public void Close() { }
            public void Open(TimeSpan timeout) => Open();
            public void Close(TimeSpan timeout) { }
            public IAsyncResult BeginOpen(AsyncCallback callback, object state) => throw new NotSupportedException();
            public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state) => throw new NotSupportedException();
            public void EndOpen(IAsyncResult result) => throw new NotSupportedException();
            public IAsyncResult BeginClose(AsyncCallback callback, object state) => throw new NotSupportedException();
            public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state) => throw new NotSupportedException();
            public void EndClose(IAsyncResult result) => throw new NotSupportedException();
            public event EventHandler Closed { add { } remove { } }
            public event EventHandler Closing { add { } remove { } }
            public event EventHandler Faulted { add { } remove { } }
            public event EventHandler Opened { add { } remove { } }
            public event EventHandler Opening { add { } remove { } }
        }
    }
}
