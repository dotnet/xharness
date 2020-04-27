// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Listeners
{
    public interface ISimpleListener
    {
        Task CompletionTask { get; }
        Task ConnectedTask { get; }
        int Port { get; }
        ILog TestLog { get; }

        void Cancel();
        void Dispose();
        void Initialize();
        void StartAsync();
    }

    public abstract class SimpleListener : ISimpleListener, IDisposable
    {
        readonly TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>();
        readonly TaskCompletionSource<bool> connected = new TaskCompletionSource<bool>();
        public ILog TestLog { get; private set; }

        protected readonly IPAddress Address = IPAddress.Any;
        protected ILog Log { get; }
        protected bool XmlOutput { get; }
        protected ILog OutputWriter { get; private set; }
        protected abstract void Start();
        protected abstract void Stop();

        public Task ConnectedTask => connected.Task;
        public int Port { get; protected set; }
        public abstract void Initialize();

        protected SimpleListener(ILog log, ILog testLog, bool xmlOutput)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            TestLog = testLog ?? throw new ArgumentNullException(nameof(testLog));
            XmlOutput = xmlOutput;
        }

        protected void Connected(string remote)
        {
            Log.WriteLine("Connection from {0} saving logs to {1}", remote, TestLog.FullPath);
            connected.TrySetResult(true);
        }

        protected void Finished(bool early_termination = false)
        {
            if (stopped.TrySetResult(early_termination))
            {
                if (early_termination)
                {
                    Log.WriteLine("Tests were terminated before completion");
                }
                else
                {
                    Log.WriteLine("Tests have finished executing");
                }
            }
        }

        public void StartAsync()
        {
            var t = new Thread(() =>
            {
                try
                {
                    Start();
                }
                catch (Exception e)
                {
                    Log.WriteLine($"{GetType().Name}: an exception occurred in processing thread: {e}");
                }
            })
            {
                IsBackground = true,
            };
            t.Start();
        }

        public bool WaitForCompletion(TimeSpan ts)
        {
            return stopped.Task.Wait(ts);
        }

        public Task CompletionTask => stopped.Task;

        public void Cancel()
        {
            connected.TrySetCanceled();
            try
            {
                // wait a second just in case more data arrives.
                if (!stopped.Task.Wait(TimeSpan.FromSeconds(1)))
                    Stop();
            }
            catch
            {
                // We might have stopped already, so just ignore any exceptions.
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            OutputWriter?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}

