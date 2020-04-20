// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Moq;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Xunit;
using System;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Listeners
{
    public class SimpleListenerFactoryTest : IDisposable
    {
        private Mock<ILog> log;
        private SimpleListenerFactory factory;

        public SimpleListenerFactoryTest()
        {
            log = new Mock<ILog>();
            factory = new SimpleListenerFactory();
        }

        public void Dispose()
        {
            log = null;
            factory = null;
        }

        [Fact]
        public void CreateNotWatchListener()
        {
            var (transport, listener, listenerTmpFile) = factory.Create(RunMode.iOS, log.Object, log.Object, true, true, true);
            Assert.Equal(ListenerTransport.Tcp, transport);
            Assert.IsType<SimpleTcpListener>(listener);
            Assert.Null(listenerTmpFile);
        }

        [Fact]
        public void CreateWatchOSSimulator()
        {
            var logFullPath = "myfullpath.txt";
            _ = log.Setup(l => l.FullPath).Returns(logFullPath);

            var (transport, listener, listenerTmpFile) = factory.Create(RunMode.WatchOS, log.Object, log.Object, true, true, true);
            Assert.Equal(ListenerTransport.File, transport);
            Assert.IsType<SimpleFileListener>(listener);
            Assert.NotNull(listenerTmpFile);
            Assert.Equal(logFullPath + ".tmp", listenerTmpFile);

            log.Verify(l => l.FullPath, Times.Once);

        }

        [Fact]
        public void CreateWatchOSDevice()
        {
            var (transport, listener, listenerTmpFile) = factory.Create(RunMode.WatchOS, log.Object, log.Object, false, true, true);
            Assert.Equal(ListenerTransport.Http, transport);
            Assert.IsType<SimpleHttpListener>(listener);
            Assert.Null(listenerTmpFile);
        }
    }
}
