// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;

namespace Microsoft.DotNet.XHarness.Common.Execution
{
    public abstract class ProcessManager : IProcessManager
    {

        #region Abstract methods

        protected abstract int Kill(int pid, int sig);
        protected abstract List<int> GetChildrenPS(ILog log, int pid);

        #endregion

        #region IProcessManager implementation

        public async Task<ProcessExecutionResult> ExecuteCommandAsync(string filename,
            IList<string> args,
            ILog log,
            TimeSpan timeout,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null)
            => await ExecuteCommandAsync(
                filename: filename,
                args: args,
                log:log,
                stdout:log,
                stderr:log,
                timeout: timeout,
                environmentVariables: environmentVariables,
                cancellationToken: cancellationToken);

        public async Task<ProcessExecutionResult> ExecuteCommandAsync(string filename,
            IList<string> args,
            ILog log,
            ILog stdout,
            ILog stderr,
            TimeSpan timeout,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null)
        {
            using var p = new Process();
            p.StartInfo.FileName = filename ?? throw new ArgumentNullException(nameof(filename));
            p.StartInfo.Arguments = StringUtils.FormatArguments(args);
            return await RunAsync(p, log, stdout, stderr, timeout, environmentVariables, cancellationToken);
        }

        public Task<ProcessExecutionResult> RunAsync(
            Process process,
            ILog log,
            TimeSpan? timeout = null,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null,
            bool? diagnostics = null)
            => RunAsync(
                process: process,
                log:log,
                stdout:log,
                stderr:log,
                timeout: timeout,
                environmentVariables: environmentVariables,
                cancellationToken: cancellationToken,
                diagnostics: diagnostics);

        public Task<ProcessExecutionResult> RunAsync(
            Process process,
            ILog log,
            ILog stdout,
            ILog stderr,
            TimeSpan? timeout = null,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null,
            bool? diagnostics = null)
            => RunAsyncInternal(
                process: process,
                log: log,
                stdout: stdout,
                stderr: stderr,
                kill:(pid, sig) => Kill(pid, sig), // lambdas are more efficient that method invoke
                getChildrenPS: (l, p) => GetChildrenPS(l, p), // same
                timeout: timeout,
                environmentVariables: environmentVariables,
                cancellationToken: cancellationToken,
                diagnostics: diagnostics);

        public Task KillTreeAsync(Process process, ILog log, bool? diagnostics = true)
            => KillTreeAsyncInternal(
                pid: process.Id,
                kill: (p, s) => Kill(p, s),
                getChildrenPS: (l, p) => GetChildrenPS(l, p),
                log: log,
                diagnostics: diagnostics);

        public Task KillTreeAsync(int pid, ILog log, bool? diagnostics = true)
            => KillTreeAsyncInternal(
                pid: pid,
                kill: (p, s) => Kill(p, s),
                getChildrenPS: (l, i) => GetChildrenPS (l, i),
                log: log,
                diagnostics: diagnostics);

        static async Task KillTreeAsyncInternal(int pid, Action<int, int> kill, Func<ILog, int, IList<int>> getChildrenPS, ILog log, bool? diagnostics = true)
        {
            var pids = getChildrenPS(log, pid);

            if (diagnostics == true)
            {
                log.WriteLine($"Pids to kill: {string.Join(", ", pids.Select((v) => v.ToString()).ToArray())}");
                using (var ps = new Process())
                {
                    log.WriteLine("Writing process list:");
                    ps.StartInfo.FileName = "ps";
                    ps.StartInfo.Arguments = "-A -o pid,ruser,ppid,pgid,%cpu=%CPU,%mem=%MEM,flags=FLAGS,lstart,rss,vsz,tty,state,time,command";
                    await RunAsyncInternal(
                        process: ps,
                        log: log,
                        stdout:log,
                        stderr:log,
                        kill: kill,
                        getChildrenPS: getChildrenPS,
                        timeout: TimeSpan.FromSeconds(5),
                        diagnostics: false);
                }

                foreach (var diagnose_pid in pids)
                {
                    var template = Path.GetTempFileName();
                    try
                    {
                        var commands = new StringBuilder();
                        using (var dbg = new Process())
                        {
                            commands.AppendLine($"process attach --pid {diagnose_pid}");
                            commands.AppendLine("thread list");
                            commands.AppendLine("thread backtrace all");
                            commands.AppendLine("detach");
                            commands.AppendLine("quit");
                            dbg.StartInfo.FileName = "/usr/bin/lldb";
                            dbg.StartInfo.Arguments = StringUtils.FormatArguments("--source", template);
                            File.WriteAllText(template, commands.ToString());

                            log.WriteLine($"Printing backtrace for pid={pid}");
                            await RunAsyncInternal(
                                process: dbg,
                                log: log,
                                stdout: log,
                                stderr:log,
                                kill: kill,
                                getChildrenPS: getChildrenPS,
                                timeout: TimeSpan.FromSeconds(30),
                                diagnostics: false);
                        }
                    }
                    finally
                    {
                        try
                        {
                            File.Delete(template);
                        }
                        catch
                        {
                            // Don't care
                        }
                    }
                }
            }

            // Send SIGABRT since that produces a crash report
            // lldb may fail to attach to system processes, but crash reports will still be produced with potentially helpful stack traces.
            for (int i = 0; i < pids.Count; i++)
                kill(pids[i], 6);

            // send kill -9 anyway as a last resort
            for (int i = 0; i < pids.Count; i++)
                kill(pids[i], 9);
        }

        protected static async Task<ProcessExecutionResult> RunAsyncInternal(
            Process process,
            ILog log,
            ILog stdout,
            ILog stderr,
            Action<int, int> kill,
            Func<ILog, int, IList<int>> getChildrenPS,
            TimeSpan? timeout = null,
            Dictionary<string, string>? environmentVariables = null,
            CancellationToken? cancellationToken = null,
            bool? diagnostics = null)
        {
            var stdout_completion = new TaskCompletionSource<bool>();
            var stderr_completion = new TaskCompletionSource<bool>();
            var rv = new ProcessExecutionResult();

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            // Make cute emojiis show up as cute emojiis in the output instead of ugly text symbols!
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            process.StartInfo.UseShellExecute = false;

            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    if (kvp.Value == null)
                    {
                        process.StartInfo.EnvironmentVariables.Remove(kvp.Key);
                    }
                    else
                    {
                        process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                }
            }

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (stdout)
                    {
                        stdout.WriteLine(e.Data);
                        stdout.Flush();
                    }
                }
                else
                {
                    stdout_completion.TrySetResult(true);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (stderr)
                    {
                        stderr.WriteLine(e.Data);
                        stderr.Flush();
                    }
                }
                else
                {
                    stderr_completion.TrySetResult(true);
                }
            };

            var sb = new StringBuilder();
            if (process.StartInfo.EnvironmentVariables != null)
            {
                var currentEnvironment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary((v) => (string)v.Key, (v) => (string?)v.Value, StringComparer.Ordinal);
                var processEnvironment = process.StartInfo.EnvironmentVariables.Cast<System.Collections.DictionaryEntry>().ToDictionary((v) => (string)v.Key, (v) => (string?)v.Value, StringComparer.Ordinal);
                var allKeys = currentEnvironment.Keys.Union(processEnvironment.Keys).Distinct();
                foreach (var key in allKeys)
                {
                    if(key == null) continue;

                    string? a = null, b = null;
                    currentEnvironment?.TryGetValue(key!, out a);
                    processEnvironment?.TryGetValue(key!, out b);
                    if (a != b)
                        sb.Append($"{key}={StringUtils.Quote(b)} ");
                }
            }
            sb.Append($"{StringUtils.Quote(process.StartInfo.FileName)} {process.StartInfo.Arguments}");
            log.WriteLine(sb);

            process.Start();
            var pid = process.Id;

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            cancellationToken?.Register(() =>
            {
                var hasExited = false;
                try
                {
                    hasExited = process.HasExited;
                }
                catch
                {
                    // Process.HasExited can sometimes throw exceptions, so
                    // just ignore those and to be safe treat it as the
                    // process didn't exit (the safe option being to not leave
                    // processes behind).
                }
                if (!hasExited)
                {
                    stderr.WriteLine($"Execution of {pid} was cancelled.");
                    kill(pid, 9);
                }
            });

            if (timeout.HasValue)
            {
                if (!await WaitForExitAsync(process, timeout.Value))
                {
                    await KillTreeAsyncInternal(process.Id, kill, getChildrenPS, log, diagnostics ?? true);
                    rv.TimedOut = true;
                    lock (stderr)
                        log.WriteLine($"{pid} Execution timed out after {timeout.Value.TotalSeconds} seconds and the process was killed.");
                }
            }
            await WaitForExitAsync(process);
            Task.WaitAll(new Task[] { stderr_completion.Task, stdout_completion.Task }, TimeSpan.FromSeconds(1));

            try
            {
                rv.ExitCode = process.ExitCode;
                log.WriteLine($"Process exited with {rv.ExitCode}");
            }
            catch (Exception e)
            {
                rv.ExitCode = 12345678;
                log.WriteLine($"Failed to get ExitCode: {e}");
            }
            return rv;
        }

        static async Task<bool> WaitForExitAsync(Process process, TimeSpan? timeout = null)
        {
            if (process.HasExited)
                return true;

            var tcs = new TaskCompletionSource<bool>();

            void ProcessExited(object? sender, EventArgs ea)
            {
                process.Exited -= ProcessExited;
                tcs.TrySetResult(true);
            }

            process.Exited += ProcessExited;
            process.EnableRaisingEvents = true;

            // Check if process exited again, in case it exited after we checked
            // the last time, but before we attached the event handler.
            if (process.HasExited)
            {
                process.Exited -= ProcessExited;
                tcs.TrySetResult(true);
                return true;
            }

            if (timeout.HasValue)
            {
                return await tcs.Task.TimeoutAfter(timeout.Value);
            }
            else
            {
                await tcs.Task;
                return true;
            }
        }
        #endregion
    }
}
