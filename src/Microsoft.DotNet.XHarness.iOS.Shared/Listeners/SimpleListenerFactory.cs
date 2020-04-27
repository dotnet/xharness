// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners
{
    public enum ListenerTransport
    {
        Tcp,
        Http,
        File,
    }

    public interface ISimpleListenerFactory
    {
        (ListenerTransport transport, ISimpleListener listener, string listenerTempFile) Create(
            RunMode mode,
            ILog log,
            ILog testLog,
            bool isSimulator,
            bool autoExit,
            bool xmlOutput,
            bool useTcpTunnel);

        ITunnelBore TunnelBore { get; }
    }

    public class SimpleListenerFactory : ISimpleListenerFactory
    {

        public ITunnelBore TunnelBore { get; private set; }

        public SimpleListenerFactory(ITunnelBore tunnelBore)
        {
            TunnelBore = tunnelBore ?? throw new ArgumentNullException(nameof(tunnelBore));
        }

        public (ListenerTransport transport, ISimpleListener listener, string listenerTempFile) Create(
            RunMode mode,
            ILog log,
            ILog testLog,
            bool isSimulator,
            bool autoExit,
            bool xmlOutput,
            bool useTcpTunnel)
        {
            string listenerTempFile = null;
            ISimpleListener listener;
            ListenerTransport transport;

            if (mode == RunMode.WatchOS)
            {
                transport = isSimulator ? ListenerTransport.File : ListenerTransport.Http;
            }
            else
            {
                transport = ListenerTransport.Tcp;
            }

            switch (transport)
            {
                case ListenerTransport.File:
                    listenerTempFile = testLog.FullPath + ".tmp";
                    listener = new SimpleFileListener(listenerTempFile, log, testLog, xmlOutput);
                    break;
                case ListenerTransport.Http:
                    listener = new SimpleHttpListener(log, testLog, autoExit);
                    break;
                case ListenerTransport.Tcp:
                    listener = new SimpleTcpListener(log, testLog, autoExit, useTcpTunnel);
                    break;
                default:
                    throw new NotImplementedException("Unknown type of listener");
            }

            return (transport, listener, listenerTempFile);
        }
    }
}
