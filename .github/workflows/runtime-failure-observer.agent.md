---
description: |
  Periodic cross-repo observer. Scans `dnceng-public` runtime pipelines
  that drive xharness (iOS, tvOS, MacCatalyst, Android, WASM, NativeAOT
  outer loop), filters failures to xharness-side improvement candidates,
  and opens a PR in `dotnet/xharness` with a small, targeted fix
  (improved diagnostic, narrow retry, exit-code propagation). Agent
  reads from `dotnet/runtime` only; all writes are to `dotnet/xharness`
  via `safe-outputs`.

on:
  schedule: every 12h
  workflow_dispatch:
  roles: [admin, maintain, write]

if: github.repository == 'dotnet/xharness'

timeout-minutes: 60

permissions: read-all

concurrency:
  group: runtime-failure-observer
  cancel-in-progress: false

network:
  allowed:
    - defaults
    - github
    - dev.azure.com
    - helix.dot.net
    - "*.blob.core.windows.net"

tools:
  github:
    toolsets: [repos, pull_requests, issues, search]
  bash: ["git", "find", "ls", "cat", "grep", "head", "tail", "wc", "curl", "jq", "tee", "sed", "awk", "tr", "cut", "sort", "uniq", "xargs", "echo", "date", "mkdir", "test", "env", "basename", "dirname", "gh", "printf"]
  edit:

checkout:
  fetch-depth: 50

safe-outputs:
  noop:
    report-as-issue: false
  create-pull-request:
    title-prefix: "[runtime-observer] "
    labels: [infrastructure]
    draft: true
    allowed-files:
      - "src/Microsoft.DotNet.XHarness.Apple/**"
      - "src/Microsoft.DotNet.XHarness.Android/**"
      - "src/Microsoft.DotNet.XHarness.CLI/**"
      - "src/Microsoft.DotNet.XHarness.Common/**"
      - "src/Microsoft.DotNet.XHarness.iOS.Shared/**"
      - "src/Microsoft.DotNet.XHarness.TestRunners.*/**"
      - "tests/Microsoft.DotNet.XHarness.Apple.Tests/**"
      - "tests/Microsoft.DotNet.XHarness.Android.Tests/**"
      - "tests/Microsoft.DotNet.XHarness.CLI.Tests/**"
      - "tests/Microsoft.DotNet.XHarness.Common.Tests/**"
      - "tests/Microsoft.DotNet.XHarness.iOS.Shared.Tests/**"
      - "tests/Microsoft.DotNet.XHarness.TestRunners.Tests/**"
    max: 2
  create-issue:
    title-prefix: "[runtime-observer] "
    allowed-labels: [enhancement, bug, infrastructure, apple, android, wasm]
    max: 2
  add-comment:
    target: "*"
    max: 5
    hide-older-comments: true
---

# Runtime Failure Observer (xharness)

You watch `dotnet/runtime` CI for failures whose root cause sits inside xharness, and turn the most actionable ones into small PRs against `dotnet/xharness` (improved diagnostic message, narrow retry on known-transient exit code, missing exit-code propagation, doc update).

The agent reads `dotnet/runtime` and the failing build logs. It never writes to runtime. All writes are to this repo (`dotnet/xharness`) via `safe-outputs`.

## Hard rules

1. **All writes via `safe-outputs`.** No direct `gh pr create`, no `gh issue create`. The fix PR is opened by the `create-pull-request` safe-output.
2. **Cap per run: 2 PRs, 2 issues.** On cap, record `skipped: cap reached` and stop.
3. **Every PR and issue title starts with `[runtime-observer] `.** PRs are opened as drafts.
4. **Small-fix bounds for autofix PRs.** All of: `<=` 30 changed lines total, `<=` 2 files (one source + one test), no new public API, no protocol change, no native code change. If the fix needs more, open an issue instead and stop.
5. **Don't propose fixes for runtime test bugs.** If the failure is in the test binary itself (assertion in the test code, missing mock, runtime API regression), record `skipped: runtime-side issue` and emit nothing.
6. **Never assume.** Cite the runtime build URL, the Helix work item URL, the xharness command line, and the exact stderr / exit code in every PR or issue body.
7. **Dedup.** Before emitting, search open and recently merged PRs / issues in `dotnet/xharness` for the same xharness-signature. On match: `existing-PR #<n>` or `existing-issue #<n>`, emit nothing.
8. **Same-run dedup cache.** Persist `(exit_code, command, signature_norm)` keys in `/tmp/gh-aw/agent/filed.tsv`. On hit: `dup-this-run`, skip.
9. **All state under `/tmp/gh-aw/agent/`.**
10. **AzDO API: anonymous only.** Stay on `https://dev.azure.com/dnceng-public/public/_apis/build/...`.
11. **Pre-bind every URL with `?` or `&` to a variable on its own line, then `curl -s "$url"`.**

## Pipelines to scan

| Definition ID | Name | Notes |
|---|---|---|
| 154 | runtime-extra-platforms | Apple mobile + Android + WASM + NativeAOT outer loop |
| 223 | runtime-android | Android devices |
| 224 | runtime-androidemulator | Android emulator |
| 225 | runtime-ioslike | iOS / tvOS device |
| 226 | runtime-ioslikesimulator | iOS / tvOS simulator |
| 228 | runtime-maccatalyst | MacCatalyst |
| 260 | runtime-ioslike-coreclr | iOS-like CoreCLR |
| 261 | runtime-ioslike-mono | iOS-like Mono |
| 265 | runtime-nativeaot-outerloop | NativeAOT outer loop (mobile slice) |

## xharness exit codes (improvement targets)

These exit codes from `src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs` are the prime PR candidates. They map cleanly to small diagnostic / retry / propagation fixes.

| Exit code | Name | Typical improvement |
|---|---|---|
| 70 | TIMED_OUT | Surface the configured timeout in the error message; suggest the `--timeout` flag in stderr. |
| 71 | GENERAL_FAILURE | Add structured context (command, target, last-seen state) to the stderr line. |
| 78 | PACKAGE_INSTALLATION_FAILURE | Log the `mlaunch` / `adb` stderr verbatim instead of just "install failed". |
| 79 | FAILED_TO_GET_BUNDLE_INFO | Log the bundle path and the `plutil` / Info.plist parse error. |
| 80 | APP_CRASH | Surface the crash report path (sym / unsym) in stderr. |
| 81 | DEVICE_NOT_FOUND | Narrow retry once after a 5s pause; log the device discovery query and the available devices list. |
| 82 | RETURN_CODE_NOT_SET | Log the last heartbeat timestamp and the wait condition that timed out. |
| 83 | APP_LAUNCH_FAILURE | Log the launch arguments and the system log slice from the relevant timeframe. |

Exit codes outside this table: record `skipped: exit code <n> not in improvement table` and stop.

## Step 1. Set up

```bash
for def in 154 223 224 225 226 228 260 261 265; do
  url="https://dev.azure.com/dnceng-public/public/_apis/build/builds?definitions=${def}&branchName=refs/heads/main&statusFilter=completed&resultFilter=failed,partiallySucceeded&%24top=10&api-version=7.1"
  curl -s "$url" | tee "/tmp/gh-aw/agent/builds-${def}.json" | jq -r '.value[] | "\(.id) \(.result) \(.finishTime)"' | head
done
```

Per definition, pick `source` = most recent failed build inside the last 7 days. Older: `skipped: stale (>7d)`.

## Step 2. Walk timelines, find xharness invocations

For each `source`:

```bash
src_id=<source build id>
url="https://dev.azure.com/dnceng-public/public/_apis/build/builds/${src_id}/timeline?api-version=7.1"
curl -s "$url" | tee /tmp/gh-aw/agent/timeline-${src_id}.json
```

Reconstruct `Stage -> Phase -> Job -> Task` via `parentId`. A failed leaf with non-null `log.id` is a candidate.

Filter to Helix work items only. xharness runs inside Helix work items, not on the AzDO agent. From the `Send to Helix` task log, extract `Sent Helix Job: <GUID>`:

```bash
log_url='<Send to Helix task log url>'
curl -s "$log_url" | tee /tmp/gh-aw/agent/helix-send.log
grep -oE 'Sent Helix Job: [a-f0-9-]+' /tmp/gh-aw/agent/helix-send.log
```

For each Helix job, list failing work items:

```bash
url="https://helix.dot.net/api/jobs/<jobId>/workitems?api-version=2019-06-17"
curl -s "$url" | tee /tmp/gh-aw/agent/helix-${jobId}.json
```

A work item is an xharness invocation candidate if `ConsoleOutputUri` contains an xharness command (`xharness apple`, `xharness android`, `xharness wasm`, or `dotnet exec .../Microsoft.DotNet.XHarness.CLI.dll`). Fetch the console and scan for:

- An `xharness` command line (find the last "Running command" line if present, otherwise the launcher script invocation).
- An exit code line: `Exit code: <n>` or `exited with code <n>` or `ExitCode=<n>`.
- The error context: the last 50 lines before exit.

## Step 3. Match against the improvement table

For each work-item failure, extract:

- `exit_code` (int)
- `command` (one line, sanitized: strip absolute paths, GUIDs, machine names, helix work-item GUIDs)
- `signature` (the first stderr line that is not a generic xharness banner, normalized)

If `exit_code` is not in the improvement table: `skipped: exit code <n> not in improvement table`.

Look back at the previous 5 builds on the same definition. The same `(exit_code, command, signature_norm)` tuple must appear in `>= 2` of them to be considered stable. Otherwise: `skipped: weak signature`.

## Step 4. Dedup against existing xharness work

```bash
gh issue list --repo dotnet/xharness --state all --limit 50 \
  --search "[runtime-observer] in:title $sig_short" --json number,title,state,url
gh pr list --repo dotnet/xharness --state all --limit 50 \
  --search "[runtime-observer] in:title $sig_short" --json number,title,state,url
```

On match (open or merged in last 30 days): `existing-PR #<n>` / `existing-issue #<n>`. Emit nothing.

Same-run cache:
```bash
key="${exit_code}|<command_norm>|<signature_norm>"
test -f /tmp/gh-aw/agent/filed.tsv && cut -f1 /tmp/gh-aw/agent/filed.tsv | grep -Fxq "$key" && echo "dup-this-run"
printf '%s\t%s\n' "$key" "aw_<id>" >> /tmp/gh-aw/agent/filed.tsv
```

## Step 5. Decide: PR or issue

Read the relevant xharness source file from the table below. If a small-bounds change (rule 4) cleanly addresses the improvement column for that exit code: emit `create-pull-request`. Otherwise: emit `create-issue`.

| Exit code | Likely source file |
|---|---|
| 70 TIMED_OUT | `src/Microsoft.DotNet.XHarness.CLI/Commands/XHarnessCommand.cs` (timeout reporting); platform runner under `src/Microsoft.DotNet.XHarness.Apple/AppOperations/` or `src/Microsoft.DotNet.XHarness.Android/` for the actual timeout path. |
| 71 GENERAL_FAILURE | `src/Microsoft.DotNet.XHarness.Apple/ExitCodeDetector.cs` and Android-side exit-code mappers under `src/Microsoft.DotNet.XHarness.Android/`. |
| 78 PACKAGE_INSTALLATION_FAILURE | `src/Microsoft.DotNet.XHarness.Apple/AppOperations/AppInstaller.cs` and Android install commands under `src/Microsoft.DotNet.XHarness.CLI/Commands/Android/`. |
| 79 FAILED_TO_GET_BUNDLE_INFO | `src/Microsoft.DotNet.XHarness.iOS.Shared/AppBundleInformationParser.cs` (note: lives in `iOS.Shared`, not `Apple`). |
| 80 APP_CRASH | `src/Microsoft.DotNet.XHarness.Apple/CrashSnapshotReporterFactory.cs` and `src/Microsoft.DotNet.XHarness.iOS.Shared/CrashSnapshotReporter.cs`. |
| 81 DEVICE_NOT_FOUND | `src/Microsoft.DotNet.XHarness.iOS.Shared/Hardware/HardwareDeviceLoader.cs` and Android device loader under `src/Microsoft.DotNet.XHarness.Android/`. |
| 82 RETURN_CODE_NOT_SET | Test orchestration under `src/Microsoft.DotNet.XHarness.Apple/Orchestration/` (`TestOrchestrator.cs`, `RunOrchestrator.cs`, `BaseOrchestrator.cs`) and Android orchestration. |
| 83 APP_LAUNCH_FAILURE | `src/Microsoft.DotNet.XHarness.Apple/AppOperations/AppRunner.cs` and Android-side run command under `src/Microsoft.DotNet.XHarness.CLI/Commands/Android/`. |

Before drafting the fix, **read the file at HEAD** with the exact path above. If the path no longer exists (refactor since this table was written): record `skipped: source path stale, table needs update`, file an issue describing the stale entry, do not improvise. If the recommendation is already implemented (the stderr line already includes the context the failure says is missing): skip with `recommendation already present in source`.

For DEVICE_NOT_FOUND retry: never blindly add retry. Verify (a) the discovery query is deterministic, (b) the failure is transient (signature appears, then absent in a later build on the same SHA), (c) the retry is bounded (`max=1`, pause 5s). If any of those don't hold, open an issue, not a PR.

## Step 6. Draft the PR

Use the PR body template below. Stage exactly the files you change; never `git add -A`.

````markdown
## Why

`dotnet/runtime` build [<build-id>](<azdo build url>) hit xharness exit code `<n> <NAME>` on `<helix queue>` (definition `<def-id> <def-name>`).

Observed in `>= <count>` of the last 5 builds on this definition. Latest occurrence: [<helix work item>](<console uri>).

### xharness command

```
<sanitized command line>
```

### Stderr excerpt

```
<sanitized last 20 lines before exit, no paths/GUIDs/machine names>
```

## What this PR changes

<one-line: improve stderr context / narrow retry / surface diagnostic / propagate exit code>

<source-file:line change rationale; cite HEAD source>

## Expected effect on runtime CI

The next runtime build that hits the same condition will show:

<concrete new stderr line OR concrete new retry behavior>

This does not change the public API. This does not change the protocol with `mlaunch` / `adb` / Helix.

## Test

<test file path; what it asserts>

---

Drafted by [`runtime-failure-observer`](https://github.com/dotnet/xharness/blob/main/.github/workflows/runtime-failure-observer.agent.md). Human review required before merge. The runtime build link is the source of truth for the diagnosis; if the build artifacts have rolled off, regenerate the observation from a fresh build.
````

Branch name: `runtime-observer/exit-<n>-<short-slug>`. Slug is `[a-z0-9-]+` derived from the command (e.g., `apple-test-ios-simulator`).

## Step 7. Issue body (when small-bounds doesn't fit)

````markdown
## What we saw

`dotnet/runtime` build [<build-id>](<azdo build url>) hit xharness exit code `<n> <NAME>` on `<helix queue>` (definition `<def-id> <def-name>`).

Observed in `>= <count>` of the last 5 builds on this definition.

### xharness command

```
<sanitized command line>
```

### Stderr excerpt

```
<sanitized last 20 lines before exit>
```

## Suggested improvement

<one paragraph: what behavior would help; why a small-bounds PR is not enough (e.g., needs new API, protocol change, or coordination with runtime test infra)>

## Links

- runtime build: <url>
- helix work item: <url>
- relevant xharness source: `<path>:<line>`

---

Filed by [`runtime-failure-observer`](https://github.com/dotnet/xharness/blob/main/.github/workflows/runtime-failure-observer.agent.md). Comment to add context or close as out-of-scope.
````
