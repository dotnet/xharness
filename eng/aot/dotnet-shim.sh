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
# Interception scope (intentionally narrow):
#
#   1. `dotnet exec <path-ending-in-Microsoft.DotNet.XHarness.CLI.dll> <args>`
#      -> exec the native xharness binary with <args> (the dll path is
#      discarded; it's only meaningful in the JIT flow).
#
#   2. Any other invocation (e.g. `dotnet --info`, `dotnet test`,
#      `dotnet exec` of some other dll) -> forward to the next real
#      `dotnet` on PATH, if one is present. If none is present, fail
#      fast with a descriptive message. This keeps the shim from
#      silently shadowing a real dotnet that future scenarios might
#      need on the worker.

set -eu

shim_dir="$(cd "$(dirname "$0")" && pwd)"
native_xharness="$shim_dir/xharness"

if [ ! -x "$native_xharness" ]; then
    echo "dotnet-shim: native xharness binary not found at '$native_xharness'" >&2
    exit 127
fi

# Case 1: "dotnet [exec] <…/Microsoft.DotNet.XHarness.CLI.dll | …/xharness> <args>"
# -> native binary. Matches:
#   * JIT shape with exec:    dotnet exec /…/Microsoft.DotNet.XHarness.CLI.dll …
#   * AOT shape with exec:    dotnet exec /…/xharness-aot/xharness …
#     (the Helix SDK xharness() bash alias inside xharness-runner.*.sh).
#   * Direct invocation:      dotnet "$XHARNESS_CLI_PATH" …
#     (the SimulatorInstaller helix-payload script uses this shape, and other
#     legacy scripts may too). When $XHARNESS_CLI_PATH points at the native
#     binary, $1 is the binary path and we still want to invoke it natively.
_first="${1:-}"
_second="${2:-}"

if [ "$_first" = "exec" ] && [ "$#" -ge 2 ]; then
    case "$_second" in
        *Microsoft.DotNet.XHarness.CLI.dll | */xharness | xharness)
            shift 2
            exec "$native_xharness" "$@"
            ;;
    esac
fi

# 'dotnet <path-to-xharness> …' (no exec keyword)
case "$_first" in
    *Microsoft.DotNet.XHarness.CLI.dll | */xharness | xharness)
        shift
        exec "$native_xharness" "$@"
        ;;
esac

# Case 2: forward everything else to a real dotnet on PATH, if any.
# Strip our own directory from PATH before resolving so we don't
# recursively re-enter this shim.
real_path=$(printf '%s' "$PATH" | tr ':' '\n' | grep -v -x -F "$shim_dir" | tr '\n' ':' | sed 's/:$//')
real_dotnet=$(PATH="$real_path" command -v dotnet 2>/dev/null || true)

if [ -n "$real_dotnet" ] && [ "$real_dotnet" != "$0" ]; then
    exec "$real_dotnet" "$@"
fi

echo "dotnet-shim: invocation does not match the xharness pattern and no real 'dotnet' is on PATH (got: dotnet $*)" >&2
exit 127

