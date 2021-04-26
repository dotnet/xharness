﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners
{
    public class SimpleTcpListener : SimpleListener, ITunnelListener
    {
        private readonly TimeSpan _retryPeriod = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _retryPeriodIncreased = TimeSpan.FromMilliseconds(250);
        private readonly TimeSpan _increaseAfter = TimeSpan.FromSeconds(20);
        private readonly TimeSpan _timeoutAfter = TimeSpan.FromMinutes(2);

        private readonly bool _autoExit;
        private readonly bool _useTcpTunnel = true;
        private readonly byte[] _buffer = new byte[16 * 1024];

        private TcpListener _server;
        private TcpClient _client;

        public TaskCompletionSource<bool> TunnelHoleThrough { get; } = new TaskCompletionSource<bool>();

        public int Port { get; private set; }

        public SimpleTcpListener(ILog log, IFileBackedLog testLog, bool autoExit, bool tunnel = false) : base(log, testLog)
        {
            _autoExit = autoExit;
            _useTcpTunnel = tunnel;
        }

        public SimpleTcpListener(int port, ILog log, IFileBackedLog testLog, bool autoExit, bool tunnel = false) : this(log, testLog, autoExit, tunnel)
        {
            Port = port;
        }

        protected override void Stop()
        {
            _client?.Close();
            _client?.Dispose();

            // _server?.Stop(); was causing hangs
            // https://github.com/dotnet/xharness/issues/73
            _server?.Server?.Shutdown(SocketShutdown.Both);
        }

        public override int InitializeAndGetPort()
        {
            if (_useTcpTunnel && Port != 0)
            {
                return Port;
            }

            _server = new TcpListener(Address, Port);
            _server.Start();

            if (Port == 0)
            {
                Port = ((IPEndPoint)_server.LocalEndpoint).Port;
            }

            if (_useTcpTunnel)
            {
                // close the listener. We have a port. This is not the best
                // way to find a free port, but there is nothing we can do
                // better than this.

                _server.Stop();
            }

            return Port;
        }

        private void StartNetworkTcp()
        {
            bool processed;

            try
            {
                do
                {
                    Log.WriteLine("Test log server listening on: {0}:{1}", Address, Port);
                    using (_client = _server.AcceptTcpClient())
                    {
                        _client.ReceiveBufferSize = _buffer.Length;
                        processed = Processing();
                    }
                } while (!_autoExit || !processed);
            }
            catch (Exception e)
            {
                if (!(e is SocketException se) || se.SocketErrorCode != SocketError.Interrupted)
                {
                    Log.WriteLine("[{0}] : {1}", DateTime.Now, e);
                }
            }
            finally
            {
                try
                {
                    _server.Stop();
                }
                finally
                {
                    Finished();
                }
            }
        }

        private void StartTcpTunnel()
        {
            if (!TunnelHoleThrough.Task.Result)
            { // do nothing until the tunnel is ready
                throw new InvalidOperationException("Tcp tunnel could not be initialized.");
            }

            bool processed;
            try
            {
                var timeout = _retryPeriod;
                int logCounter = 0;
                var watch = Stopwatch.StartNew();
                const string address = "127.0.0.1";

                while (true)
                {
                    try
                    {
                        _client = new TcpClient(address, Port);
                        Log.WriteLine($"Test log server listening on: {address}:{Port}");

                        // let the device know we are ready!
                        var stream = _client.GetStream();
                        var ping = Encoding.UTF8.GetBytes("ping");
                        stream.Write(ping, 0, ping.Length);
                        break;

                    }
                    catch (SocketException ex)
                    {
                        if (timeout == _retryPeriod && watch.Elapsed > _increaseAfter)
                        {
                            // Switch to a 250 ms timeout after 20 seconds
                            timeout = _retryPeriodIncreased;
                        }
                        else if (watch.Elapsed > _timeoutAfter)
                        {
                            // Give up after 2 minutes.
                            throw ex;
                        }

                        if ((++logCounter) % 5 == 0)
                        {
                            Log.WriteLine(
                                $"Could not connect to the TCP tunnel on {address}:{Port}. " +
                                $"Retrying in {(int)timeout.TotalMilliseconds} ms periods...");
                        }

                        Thread.Sleep(timeout);
                    }
                }

                do
                {
                    _client.ReceiveBufferSize = _buffer.Length;
                    processed = Processing();
                } while (!_autoExit || !processed);
            }
            catch (Exception e)
            {
                if (!(e is SocketException se) || se.SocketErrorCode != SocketError.Interrupted)
                {
                    Log.WriteLine("[{0}] : {1}", DateTime.Now, e);
                }
            }
            finally
            {
                Finished();
            }
        }

        protected override void Start()
        {
            if (_useTcpTunnel)
            {
                StartTcpTunnel();
            }
            else
            {
                StartNetworkTcp();
            }
        }

        private bool Processing()
        {
            Connected(_client.Client.RemoteEndPoint.ToString());

            // now simply copy what we receive
            int i;
            int total = 0;
            NetworkStream stream = _client.GetStream();
            while ((i = stream.Read(_buffer, 0, _buffer.Length)) != 0)
            {
                TestLog.Write(_buffer, 0, i);
                TestLog.Flush();
                total += i;
            }

            if (total < 16)
            {
                // This wasn't a test run, but a connection from the app (on device) to find
                // the ip address we're reachable on.
                return false;
            }

            return true;
        }
    }
}
