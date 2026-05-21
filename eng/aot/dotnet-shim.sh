#!/usr/bin/env bash
# Tiny shim used by the NativeAOT publish flow when running under the Helix
# SDK's xharness-runner.*.sh scripts. The Helix SDK ships a bash `xharness`
# alias that unconditionally invokes:
#
#     dotnet exec "$XHARNESS_CLI_PATH" "$@"
#
# (see xharness-runner.apple.sh / xharness-runner.android.sh inside the
# Microsoft.DotNet.Helix.Sdk package). Under the NativeAOT slice we have
# no managed Microsoft.DotNet.XHarness.CLI.dll and no .NET runtime on the
# Helix worker — only a native 'xharness' binary published next to this
# shim. By placing this script on PATH ahead of any real `dotnet`, the
# helper alias's "dotnet exec <something> <args>" call becomes a direct
# invocation of the native binary with the same args.
#
# Layout this script assumes (this is exactly what `dotnet publish
# -p:XHarnessNativeAot=true` produces and what gets shipped to Helix as a
# correlation payload at $HELIX_CORRELATION_PAYLOAD/xharness-aot):
#
#   xharness-aot/
#     dotnet           <-- this script
#     xharness         <-- the NativeAOT binary
#     runtimes/any/native/...
#
# Any `dotnet` invocation that isn't of the form `dotnet exec <dll> <args>`
# fails fast with a descriptive message — the shim deliberately does NOT
# try to be a general-purpose dotnet CLI replacement.

set -eu

shim_dir="$(cd "$(dirname "$0")" && pwd)"
native_xharness="$shim_dir/xharness"

if [ ! -x "$native_xharness" ]; then
    echo "dotnet-shim: native xharness binary not found at '$native_xharness'" >&2
    exit 127
fi

if [ "${1:-}" != "exec" ]; then
    echo "dotnet-shim: only 'dotnet exec <managed-dll> <args>' is supported by this shim (got: dotnet $*)" >&2
    exit 64
fi

shift                          # consume 'exec'
if [ "$#" -lt 1 ]; then
    echo "dotnet-shim: 'dotnet exec' requires a path argument" >&2
    exit 64
fi
shift                          # consume the managed dll path (ignored; the shim always runs the native binary)

exec "$native_xharness" "$@"
