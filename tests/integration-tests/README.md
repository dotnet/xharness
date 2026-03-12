# Integration tests

This folder includes integration tests projects for different support platforms (iOS, Android, WASM).
They are used in end-to-end testing scenarios and are referenced from `azure-pipelines-public.yml` E2E templates.

In the relevant `*.proj` files one can configure various setting for execution on Helix like:
- configuring the Helix queue (e.g., `osx.15.amd64.iphone.open` via `HelixTargetQueue` item group)
- app bundle to download, send to Helix and test (e.g., `System.Buffers.Tests.app`)
- etc.

## Testing on scouting queue

NOTE: This is Apple-specific but can be applied to other platforms as well

There are two test projects which can be used on scouting queues which are not used by default:

- Apple/Simulator.Scouting.Tests.proj
- Apple/Simulator.Scouting.Commands.Tests.proj

When desired, these can be included in the `azure-pipelines-public.yml` so that the CI runs them on a desired scouting queue (check the `HelixTargetQueue` setting) with a particular version of Xcode.
