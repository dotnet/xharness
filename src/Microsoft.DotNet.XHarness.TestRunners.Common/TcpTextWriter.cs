// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// this is an adaptation of NUnitLite's TcpWriter.cs with a additional
// overrides and with network-activity UI enhancement
// This code is a small modification of
// https://github.com/spouliot/Touch.Unit/blob/master/NUnitLite/TouchRunner/TcpTextWriter.cs

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Microsoft.DotNet.XHarness.TestRunners.Common
{
    internal class TcpTextWriter : TextWriter
    {
        private readonly TcpClient _client;
        private readonly StreamWriter _writer;

        private static string SelectHostName(string[] names, int port)
        {
            if (names.Length == 0)
            {
                return null;
            }

            if (names.Length == 1)
            {
                return names[0];
            }

            object lock_obj = new object();
            string result = null;
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

        public TcpTextWriter(string hostName, int port)
        {
            if ((port < 0) || (port > ushort.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between 0 and {ushort.MaxValue}");
            }

            if (hostName == null)
            {
                throw new ArgumentNullException(nameof(hostName));
            }

            HostName = SelectHostName(hostName.Split(','), port);
            Port = port;

            _client = new TcpClient(HostName, port);
            _writer = new StreamWriter(_client.GetStream());
        }

        public string HostName { get; private set; }

        public int Port { get; private set; }

        // we override everything that StreamWriter overrides from TextWriter

        public override System.Text.Encoding Encoding => Encoding.UTF8;

        public override void Close() => _writer.Close();

        protected override void Dispose(bool disposing) => _writer.Dispose();

        public override void Flush() => _writer.Flush();

        // minimum to override - see http://msdn.microsoft.com/en-us/library/system.io.textwriter.aspx
        public override void Write(char value) => _writer.Write(value);

        public override void Write(char[] buffer) => _writer.Write(buffer);

        public override void Write(char[] buffer, int index, int count) => _writer.Write(buffer, index, count);

        public override void Write(string value) => _writer.Write(value);

        // special extra override to ensure we flush data regularly

        public override void WriteLine()
        {
            _writer.WriteLine();
            _writer.Flush();
        }
    }
}
