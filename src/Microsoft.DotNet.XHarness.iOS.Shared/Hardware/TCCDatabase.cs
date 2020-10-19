// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware
{

    public interface ITCCDatabase
    {
        Task<bool> AgreeToPromptsAsync(string simRuntime, string dataPath, string udid, ILog log, params string[] bundle_identifiers);
        int GetTCCFormat(string simRuntime);
    }

    public class TCCDatabase : ITCCDatabase
    {
        private const string IOSSimRuntimePrefix = "com.apple.CoreSimulator.SimRuntime.iOS-";
        private const string TvOSSimRuntimePrefix = "com.apple.CoreSimulator.SimRuntime.tvOS-";
        private const string WatchOSRuntimePrefix = "com.apple.CoreSimulator.SimRuntime.watchOS-";
        private readonly IMlaunchProcessManager _processManager;

        public TCCDatabase(IMlaunchProcessManager processManager)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        }

        public int GetTCCFormat(string simRuntime)
        {
            // v1: < iOS 9
            // v2: >= iOS 9 && < iOS 12
            // v3: >= iOS 12
            // v4: >= iOS 14
            if (simRuntime.StartsWith(IOSSimRuntimePrefix, StringComparison.Ordinal))
            {
                var v = Version.Parse(simRuntime.Substring(IOSSimRuntimePrefix.Length).Replace('-', '.'));
                if (v.Major >= 14)
                {
                    return 4;
                }
                else if (v.Major >= 12)
                {
                    return 3;
                }
                else if (v.Major >= 9)
                {
                    return 2;
                }
                else
                {
                    return 1;
                }
            }
            else if (simRuntime.StartsWith(TvOSSimRuntimePrefix, StringComparison.Ordinal))
            {
                var v = Version.Parse(simRuntime.Substring(TvOSSimRuntimePrefix.Length).Replace('-', '.'));
                if (v.Major >= 14)
                {
                    return 4;
                }
                else if (v.Major >= 12)
                {
                    return 3;
                }
                else
                {
                    return 2;
                }
            }
            else if (simRuntime.StartsWith(WatchOSRuntimePrefix, StringComparison.Ordinal))
            {
                var v = Version.Parse(simRuntime.Substring(WatchOSRuntimePrefix.Length).Replace('-', '.'));
                if (v.Major >= 7)
                {
                    return 4;
                }
                else if (v.Major >= 5)
                {
                    return 3;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        public async Task<bool> AgreeToPromptsAsync(string simRuntime, string TCCDb, string udid, ILog log, params string[] bundleIdentifiers)
        {
            if (bundleIdentifiers == null || bundleIdentifiers.Length == 0)
            {
                log.WriteLine("No bundle identifiers given when requested permission editing.");
                return false;
            }

            var sim_services = new string[]
            {
                "kTCCServiceAll", // You'd think 'All' means all prompts, but some prompts still show up.
				"kTCCServiceAddressBook",
                "kTCCServiceCalendar",
                "kTCCServicePhotos",
                "kTCCServiceMediaLibrary",
                "kTCCServiceMicrophone",
                "kTCCServiceUbiquity",
                "kTCCServiceWillow"
            };

            var failure = false;
            var tcc_edit_timeout = 3;
            var watch = new Stopwatch();
            watch.Start();
            var format = GetTCCFormat(simRuntime);
            if (format >= 4)
            {
                // We don't care if booting fails (it'll fail if it's already booted for instance)
                await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "boot", udid }, log, TimeSpan.FromMinutes(1));

                // execute 'simctl privacy <udid> grant all <bundle identifier>' for each bundle identifier
                foreach (var bundle_identifier in bundleIdentifiers)
                {
                    foreach (var bundle_id in new[] { bundle_identifier, bundle_identifier + ".watchkitapp" })
                    {
                        foreach (var service in sim_services)
                        {
                            var args = new List<string>
                            {
                                "privacy",
                                udid,
                                "grant",
                                service,
                                bundle_id
                            };
                            var rv = await _processManager.ExecuteXcodeCommandAsync("simctl", args, log, TimeSpan.FromSeconds(30));
                            if (!rv.Succeeded)
                            {
                                failure = true;
                                break;
                            }
                        }
                    }

                    if (failure)
                    {
                        break;
                    }
                }
            }
            else
            {
                do
                {
                    if (failure)
                    {
                        log.WriteLine("Failed to edit TCC.db, trying again in 1 second... ", (int)(tcc_edit_timeout - watch.Elapsed.TotalSeconds));
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                    failure = false;
                    foreach (var bundle_identifier in bundleIdentifiers)
                    {
                        var args = new List<string>();
                        var sql = new System.Text.StringBuilder("\n");
                        args.Add(TCCDb);
                        foreach (var bundle_id in new[] { bundle_identifier, bundle_identifier + ".watchkitapp" })
                        {
                            foreach (var service in sim_services)
                            {
                                switch (format)
                                {
                                    case 1:
                                        // CREATE TABLE access (service TEXT NOT NULL, client TEXT NOT NULL, client_type INTEGER NOT NULL, allowed INTEGER NOT NULL, prompt_count INTEGER NOT NULL, csreq BLOB, CONSTRAINT key PRIMARY KEY (service, client, client_type));
                                        sql.AppendFormat("DELETE FROM access WHERE service = '{0}' AND client = '{1}';\n", service, bundle_id);
                                        sql.AppendFormat("INSERT INTO access VALUES('{0}','{1}',0,1,0,NULL);\n", service, bundle_id);
                                        break;
                                    case 2:
                                        // CREATE TABLE access (service	TEXT NOT NULL, client TEXT NOT NULL, client_type INTEGER NOT NULL, allowed INTEGER NOT NULL, prompt_count INTEGER NOT NULL, csreq BLOB, policy_id INTEGER, PRIMARY KEY (service, client, client_type), FOREIGN KEY (policy_id) REFERENCES policies(id) ON DELETE CASCADE ON UPDATE CASCADE);
                                        sql.AppendFormat("DELETE FROM access WHERE service = '{0}' AND client = '{1}';\n", service, bundle_id);
                                        sql.AppendFormat("INSERT INTO access VALUES('{0}','{1}',0,1,0,NULL,NULL);\n", service, bundle_id);
                                        break;
                                    case 3: // Xcode 10+
                                        // CREATE TABLE access (service TEXT NOT NULL, client TEXT NOT NULL, client_type INTEGER NOT NULL, allowed INTEGER NOT NULL, prompt_count INTEGER NOT NULL, csreq BLOB, policy_id INTEGER, indirect_object_identifier_type INTEGER, indirect_object_identifier TEXT, indirect_object_code_identity BLOB, flags INTEGER, last_modified  INTEGER NOT NULL DEFAULT (CAST(strftime('%s','now') AS INTEGER)), PRIMARY KEY (service, client, client_type, indirect_object_identifier), FOREIGN KEY (policy_id) REFERENCES policies(id) ON DELETE CASCADE ON UPDATE CASCADE)
                                        sql.AppendFormat("INSERT OR REPLACE INTO access VALUES('{0}','{1}',0,1,0,NULL,NULL,NULL,'UNUSED',NULL,NULL,{2});\n", service, bundle_id, DateTimeOffset.Now.ToUnixTimeSeconds());
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                        }

                        args.Add(sql.ToString());

                        var rv = await _processManager.ExecuteCommandAsync("sqlite3", args, log, TimeSpan.FromSeconds(5));
                        if (!rv.Succeeded)
                        {
                            failure = true;
                            break;
                        }
                    }
                } while (failure && watch.Elapsed.TotalSeconds <= tcc_edit_timeout);
            }

            if (failure)
            {
                log.WriteLine("Failed to edit TCC.db, the test run might hang due to permission request dialogs");
            }
            else
            {
                log.WriteLine("Successfully edited TCC.db");
            }

            log.WriteLine("Current TCC database contents:");
            await _processManager.ExecuteCommandAsync("sqlite3", new[] { TCCDb, ".dump" }, log, TimeSpan.FromSeconds(5));

            return !failure;
        }
    }
}
