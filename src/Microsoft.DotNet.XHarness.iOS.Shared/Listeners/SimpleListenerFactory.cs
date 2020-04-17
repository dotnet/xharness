﻿// Licensed to the .NET Foundation under one or more agreements.
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
        (ListenerTransport transport, ISimpleListener listener, string listenerTempFile) Create(RunMode mode,
            ILog log,
            ILog listenerLog,
            bool isSimulator,
            bool autoExit,
            bool xmlOutput);
    }

    public class SimpleListenerFactory : ISimpleListenerFactory
    {

        public (ListenerTransport transport, ISimpleListener listener, string listenerTempFile) Create(RunMode mode,
            ILog log,
            ILog listenerLog,
            bool isSimulator,
            bool autoExit,
            bool xmlOutput)
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
                    listenerTempFile = listenerLog.FullPath + ".tmp";
                    listener = new SimpleFileListener(listenerTempFile, log, listenerLog, xmlOutput);
                    break;
                case ListenerTransport.Http:
                    listener = new SimpleHttpListener(log, listenerLog, autoExit, xmlOutput);
                    break;
                case ListenerTransport.Tcp:
                    listener = new SimpleTcpListener(log, listenerLog, autoExit, xmlOutput);
                    break;
                default:
                    throw new NotImplementedException("Unknown type of listener");
            }

            return (transport, listener, listenerTempFile);
        }
    }
}
