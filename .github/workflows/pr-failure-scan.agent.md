---
description: |
  On-demand PR CI triage. A maintainer invokes `/pr-failure-scan` on a
  `dotnet/xharness` pull request. The agent follows the shared
  `pr-failure-scan` skill: it inspects the PR's current CI, isolates the
  failures Build Analysis did not already recognize, checks for existing
  matching issues, assesses whether each failure looks PR-caused, and posts
  one consolidated AI-generated triage comment. Read-only against CI; the
  only write is a single PR comment via `safe-outputs`.

on:
  slash_command:
    name: pr-failure-scan
  roles: [admin, maintain, write]

if: |
  github.repository == 'dotnet/xharness' &&
  github.event.issue.pull_request

timeout-minutes: 30

permissions: read-all

concurrency:
  group: pr-failure-scan-${{ github.event.issue.number }}
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
    toolsets: [repos, pull_requests, issues, actions, search]
  bash: ["git", "find", "ls", "cat", "grep", "head", "tail", "wc", "curl", "jq", "tee", "sed", "awk", "tr", "cut", "sort", "uniq", "xargs", "echo", "date", "mkdir", "test", "env", "basename", "dirname", "gh", "printf"]

imports:
  - uses: .github/skills/pr-failure-scan/SKILL.md

safe-outputs:
  add-comment:
    target: "triggering"
    max: 1
---

# PR Failure Scan (xharness)

A maintainer invoked `/pr-failure-scan` on PR #${{ github.event.issue.number }}. Run the
imported **PR Failure Scan Skill** procedure against that PR.

## Execution notes for this workflow

The skill was written for interactive use. In this workflow it runs unattended, triggered by a
slash command from a maintainer who has already opted in. Adapt the skill as follows:

1. **Target.** The PR is `#${{ github.event.issue.number }}` in `dotnet/xharness`. Resolve its
   head SHA, base branch, state, and draft status before doing anything else. If the PR is closed,
   post nothing and report that only open PRs are supported.
2. **No interactive approval.** This workflow instruction overrides the imported skill's Step 7
   and its "do not post without explicit confirmation" rule. The slash-command invocation by a
   maintainer is the approval. Produce the single consolidated comment and emit it directly through
   the `add-comment` safe output. Do not attempt `gh pr comment` or `gh issue comment`; all writes go
   through `safe-outputs`.
3. **Exactly one comment.** One run produces at most one PR comment. Keep the AI-generated note at the
   top of the body. Omit empty sections. If there are no in-scope failures, emit no comment and say so
   in the run log.
4. **Stay read-only otherwise.** Do not create issues, KBEs, or follow-up PRs. Do not push commits.
5. **Scope.** `dotnet/xharness` only.
