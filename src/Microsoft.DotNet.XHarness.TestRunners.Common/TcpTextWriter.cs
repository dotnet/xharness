// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// this is an adaptation of NUnitLite's TcpWriter.cs with a additional
// overrides and with network-activity UI enhancement
// This code is a small modification of
// https://github.com/spouliot/Touch.Unit/blob/master/NUnitLite/TouchRunner/TcpTextWriter.cs

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common
{
    internal class TcpTextWriter : TextWriter
    {
        private static readonly TimeSpan s_connectionAwaitPeriod = TimeSpan.FromMinutes(1);

        private TcpClient? _client = null;
        private StreamWriter? _writer = null;

        public void InitializeTunnelConnection(int port)
        {
            if ((port < 0) || (port > ushort.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between 0 and {ushort.MaxValue}");
            }

            var server = new TcpListener(IPAddress.Any, port);
            server.Server.ReceiveTimeout = 5000;
            server.Start();
            var watch = Stopwatch.StartNew();

            while (!server.Pending())
            {
                if (watch.Elapsed > s_connectionAwaitPeriod)
                {
                    throw new Exception($"No inbound TCP connection after {(int) s_connectionAwaitPeriod.TotalSeconds} seconds");
                }

                Thread.Sleep(100);
            }

            _client = server.AcceptTcpClient();

            // Block until we have the ping from the client side
            byte[] buffer = new byte[16 * 1024];
            var stream = _client.GetStream();
            while ((_ = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                var message = Encoding.UTF8.GetString(buffer);
                if (message.Contains("ping"))
                {
                    break;
                }
            }

            _writer = new StreamWriter(_client.GetStream());
        }

        public void InitializeDirectConnection(string hostName, int port)
        {
            if (hostName is null)
            {
                throw new ArgumentNullException(nameof(hostName));
            }

            if ((port < 0) || (port > ushort.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between 0 and {ushort.MaxValue}");
            }

            hostName = SelectHostName(hostName.Split(','), port);

            _client = new TcpClient(hostName, port);
            _writer = new StreamWriter(_client.GetStream());
        }

        // we override everything that StreamWriter overrides from TextWriter

        public override Encoding Encoding => Encoding.UTF8;

        public override void Close()
        {
            ValidateWriter();
            _writer.Close();
        }

        protected override void Dispose(bool disposing) => _writer?.Dispose();

        public override void Flush()
        {
            ValidateWriter();
            _writer.Flush();
        }

        // minimum to override - see http://msdn.microsoft.com/en-us/library/system.io.textwriter.aspx
        public override void Write(char value)
        {
            ValidateWriter();
            _writer.Write(value);
        }

        public override void Write(char[]? buffer)
        {
            ValidateWriter();
            _writer.Write(buffer);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            ValidateWriter();
            _writer.Write(buffer, index, count);
        }

        public override void Write(string? value)
        {
            ValidateWriter();
            _writer.Write(value);
        }

        // special extra override to ensure we flush data regularly

        public override void WriteLine()
        {
            ValidateWriter();
            _writer.WriteLine();
            _writer.Flush();
        }

        private static string SelectHostName(string[] names, int port)
        {
            if (names.Length == 1)
            {
                return names[0];
            }

            object lock_obj = new object();
            string? result = null;
            int failures = 0;

            using (var evt = new ManualResetEvent(false))
            {
                for (int i = names.Length - 1; i >= 0; i--)
                {
                    var name = names[i];
                    ThreadPool.QueueUserWorkItem((v) =>
                    {
                        try
                        {
                            var client = new TcpClient(name, port);
                            using (var writer = new StreamWriter(client.GetStream()))
                            {
                                writer.WriteLine("ping");
                            }
                            lock (lock_obj)
                            {
                                if (result == null)
                                {
                                    result = name;
                                }
                            }
                            evt.Set();
                        }
                        catch (Exception)
                        {
                            lock (lock_obj)
                            {
                                failures++;
                                if (failures == names.Length)
                                {
                                    evt.Set();
                                }
                            }
                        }
                    });
                }

                // Wait for 1 success or all failures
                evt.WaitOne();
            }

            if (result == null)
            {
                throw new InvalidOperationException("Couldn't connect to any of the hostnames.");
            }

            return result;
        }

        [MemberNotNull(nameof(_writer))]
        private void ValidateWriter()
        {
            if (_writer == null)
            {
                throw new InvalidOperationException("Please initialize the writer before usage by calling one of the Initialize*() methods.");
            }
        }
    }
}
