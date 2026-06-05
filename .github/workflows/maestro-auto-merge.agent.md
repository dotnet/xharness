---
description: |
  Reviewer for Maestro dependency-update PRs. Labels safe ones
  `auto-merge-candidate` so `maestro-auto-merge-finalize.yml` can squash-merge
  them once the PR has been idle for at least 4 hours. Never approves, merges,
  or pushes commits.

on:
  pull_request:
    types: [opened, synchronize, ready_for_review]
  roles: [admin, maintain, write]

if: |
  github.repository == 'dotnet/xharness' &&
  (github.event.pull_request.user.login == 'dotnet-maestro' ||
   github.event.pull_request.user.login == 'dotnet-maestro[bot]')

timeout-minutes: 10

permissions: read-all

network:
  allowed:
    - defaults
    - github
    - dotnet

tools:
  github:
    toolsets: [repos, pull_requests, actions]
  bash: true

safe-outputs:
  noop:
    report-as-issue: false
  add-labels:
    allowed: [auto-merge-candidate, maestro-bump]
  remove-labels:
    allowed: [auto-merge-candidate, maestro-bump]
  add-comment:
    target: "triggering"
    max: 1
    hide-older-comments: true
---

# Maestro Auto-Merge Reviewer

Decide whether PR #${{ github.event.pull_request.number }} is a safe Maestro dependency bump. If every gate below passes, label `auto-merge-candidate`. The finalizer merges it once the PR has been idle for at least 4 hours.

## Hard rules

1. Do not approve. Do not merge. Do not push.
2. One comment per run. If the previous bot comment is identical, do not repost.
3. Skip drafts.
4. **Revoke on regression.** If a `synchronize` event re-runs this workflow and any gate now fails AND `auto-merge-candidate` is currently applied: remove `auto-merge-candidate` first, then post the failure comment. This prevents the finalizer from squashing a now-out-of-scope diff.

## Gates

1. **Author.** Must be `dotnet-maestro` or `dotnet-maestro[bot]`. Title must start with `Update dependencies from`. Otherwise `noop`.
2. **Label.** Apply `maestro-bump`.
3. **Diff scope.** Files changed must be a subset of `eng/Version.Details.xml`, `eng/Versions.props`, `global.json`. Anything else: comment the out-of-scope paths and stop.
4. **Channel coherency.** Every updated `<Dependency>` in `eng/Version.Details.xml` must have populated `<Uri>` and `<Sha>` and come from a single channel. Otherwise comment the offending dependency and stop.
5. **Version exists on nuget.org.** For each updated package: `curl -s https://api.nuget.org/v3-flatcontainer/<pkg>/index.json` and confirm the new version is listed. Missing: comment the `(pkg, version)` pairs and stop.
6. **No downgrades.** For each updated `<Dependency>` in the `eng/Version.Details.xml` diff, the new `Version` must be strictly greater than the old one (semver compare, prerelease-aware). If any package is being downgraded or pinned to an equal version, comment the offending `(pkg, old -> new)` pairs and stop. We never auto-merge a dependency downgrade, even if Maestro proposes one.
7. **CI.** `gh pr checks <PR>`. If any required check is `failure` or `cancelled`: comment the failing names and stop (do not label). Pending or all-green: continue. The finalize job re-checks required CI and only merges once it is fully green, so the reviewer does not need to wait for CI here.
8. **Magnitude.** Any major-version bump: comment that a human must confirm and stop.
9. **Pass.** Apply `auto-merge-candidate`. Post one comment with: channel name, package count, largest bump, and the rule that finalize will merge once required CI is green and the PR has been idle for 4 hours.
