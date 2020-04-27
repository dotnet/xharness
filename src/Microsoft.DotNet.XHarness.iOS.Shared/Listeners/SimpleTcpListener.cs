// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners
{
    public class SimpleTcpListener : SimpleListener, ITunnelListener
    {
        private const int TimeOutInit = 100;
        private const int TimeOutIncrement = 250;

        private readonly bool _autoExit;
        private readonly bool _useTcpTunnel = true;
        private readonly byte[] _buffer = new byte[16 * 1024];

        private TcpListener _server;
        private TcpClient _client;

        public TaskCompletionSource<bool> TunnelHoleThrough { get; } = new TaskCompletionSource<bool>();

        public SimpleTcpListener(ILog log, ILog testLog, bool autoExit, bool tunnel = false) : base(log, testLog)
        {
            _autoExit = autoExit;
            _useTcpTunnel = tunnel;
        }

        public SimpleTcpListener(int port, ILog log, ILog testLog, bool autoExit, bool tunnel = false) : this(log, testLog, autoExit, tunnel)
        {
            Port = port;
        }

        protected override void Stop()
        {
            _client?.Close();
            _client?.Dispose();
            _server?.Stop();
        }

        public override void Initialize()
        {
            if (_useTcpTunnel && Port != 0)
            {
                return;
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
                var se = e as SocketException;
                if (se == null || se.SocketErrorCode != SocketError.Interrupted)
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
                int timeout = TimeOutInit; ;
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                while (true)
                {
                    try
                    {
                        _client = new TcpClient("localhost", Port);
                        Log.WriteLine("Test log server listening on: {0}:{1}", Address, Port);
                        // let the device know we are ready!
                        var stream = _client.GetStream();
                        var ping = Encoding.UTF8.GetBytes("ping");
                        stream.Write(ping, 0, ping.Length);
                        break;

                    }
                    catch (SocketException ex)
                    {
                        if (timeout == TimeOutInit && watch.ElapsedMilliseconds > 20000)
                        {
                            timeout = TimeOutIncrement; // Switch to a 250ms timeout after 20 seconds
                        }
                        else if (watch.ElapsedMilliseconds > 120000)
                        {
                            // Give up after 2 minutes.
                            throw ex;
                        }
                        Log.WriteLine($"Could not connect to TCP tunnel. Retrying in {timeout} milliseconds.");
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
                var se = e as SocketException;
                if (se == null || se.SocketErrorCode != SocketError.Interrupted)
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
