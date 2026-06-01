---
name: pr-failure-scan
description: >
  Analyze a dotnet/xharness pull request's CI failures, identify failures not
  already recognized by Build Analysis, search for existing matching issues,
  assess whether each failure is likely caused by the PR, and prepare a single
  AI-generated PR comment for explicit user approval before posting.
---

# PR Failure Scan Skill

Use this skill for requests like:

- `/pr-failure-scan <PR URL>`
- `/pr-failure-scan <PR number>`
- `analyze PR <number> CI failures`
- `scan this xharness PR for Build Analysis misses`

This skill is **PR-targeted** and scoped to **`dotnet/xharness`**.

Its job is to analyze one PR's CI state and produce **one consolidated PR
comment draft** that focuses on failures Build Analysis did **not** already
recognize, while also identifying failures that appear to already be tracked by
existing repository issues.

This skill does **not**:

- create Known Build Error issues,
- draft or file KBEs,
- create follow-up PRs,
- post multiple comments for one run.

If the skill is run again for the same PR, it should create a **new**
AI-generated comment rather than editing a prior one.

## Step 0: Parse input

Accept one of:

- a PR number,
- a PR URL,
- text containing a PR reference.

The source repository is always `dotnet/xharness`.

Examples:

- `/pr-failure-scan 1234`
- `/pr-failure-scan https://github.com/dotnet/xharness/pull/1234`

## Step 1: Resolve PR metadata and commenter identity

Resolve all of:

1. PR title
2. PR author login
3. PR URL
4. head SHA
5. base branch
6. state
7. draft status
8. currently authenticated GitHub user

The authenticated user matters because any posted PR comment will appear under
that user's account.

If the PR is closed, explain that the skill only supports open PRs and stop.

## Step 2: Gather PR CI context

Use the latest completed check runs for the PR head SHA.

Collect all of:

1. The `Build Analysis` check run payload from the GitHub REST API.
2. The non-success CI check runs for the PR head SHA.
3. The AzDO build URLs linked from those check runs.

If `Build Analysis` is missing, say so explicitly and continue with the raw CI
evidence instead of pretending Build Analysis passed or analyzed the PR.

## Step 3: Decide which failures are in scope

Build the candidate list from two sources:

1. **Build Analysis unknowns on analyzed pipelines.**
   - Parse the `Build Analysis` check text for `Create issue in this repo`
     links or equivalent unknown-failure markers and use the surrounding text
     as the failure context.
   - Treat each such entry as an in-scope candidate.
2. **Failed pipelines excluded from Build Analysis.**
   - Parse the `Build Analysis` warning section listing pipelines excluded from
     analysis.
   - For each excluded pipeline that also failed on this PR, inspect the AzDO
     build, timeline, and relevant logs to derive a concrete failure candidate.

Skip failures that Build Analysis already treated as known.

If Build Analysis and the raw PR checks disagree, prefer the raw CI evidence
and call out the disagreement in the draft comment and final chat response.

If there are no in-scope failures, report that result in chat and do **not**
post a PR comment.

## Step 4: Analyze each candidate failure

For each candidate failure, gather the most concrete evidence available:

- the Build Analysis excerpt,
- the failed timeline record,
- the relevant build or task log,
- Helix console details when accessible,
- any other directly relevant PR or build context,
- matching `dotnet/xharness` issues when they appear to describe the same
  failure.

Search repository issues for likely matches. Do not limit this to Known Build
Error issues. Search open issues first, then broaden only if needed. Try
variations such as:

1. the most specific failure text,
2. assertion or exception text,
3. test name or test class,
4. failing component, command, or target,
5. shortened stable failure-family terms when the full text is too specific.

When you find a plausible issue match, verify that it actually describes the
same failure shape rather than a nearby symptom.

For each candidate, answer these questions:

1. What is the concrete failure signature?
2. Which leg, build, or test failed?
3. Why does this appear to be outside what Build Analysis recognized?
4. Does an existing `dotnet/xharness` issue appear to already track this
   problem?
5. Is the failure **likely caused by the PR**, **likely unrelated / pre-existing**,
   or **unclear**?
6. What evidence supports that assessment?

Use evidence-based reasoning. Prefer concrete signals such as:

- files changed by the PR overlapping with the failing area,
- compiler or test failures clearly introduced by changed code,
- failures in unrelated infrastructure or historically flaky legs,
- repeated failures that do not line up with the PR's modified components,
- failure text that points to environment or queue issues rather than product
  behavior.

Do not overstate certainty. If the evidence is mixed, mark the result
**unclear** and explain why.

## Step 5: Classify outcomes

Place each candidate into exactly one of these buckets:

- **already tracked problem**
- **likely PR-caused**
- **likely not PR-caused**
- **unclear**
- **not handled**

Use **not handled** only when the logs or evidence are too incomplete to give a
useful assessment. In that case, say what evidence is missing.

Use **already tracked problem** when a repository issue is a strong match for
the same failure shape. Include the issue number, URL, and why it appears to
match. A tracked issue does not automatically prove the failure is unrelated to
the PR, but if you choose this bucket you should explain why the existing issue
is the most useful summary outcome.

## Step 6: Draft one consolidated PR comment

Prepare exactly one draft PR comment for the run. The comment must be clearly
marked as AI-generated.

Use a structure like this:

````markdown
> [!NOTE]
> AI-generated CI triage prepared with GitHub Copilot. Please verify details
> before taking action.

I reviewed the current CI failures for this PR that Build Analysis did not
already recognize.

## Already tracked problems
- `<leg or build>` - `<short failure summary>`
  - Tracked by: `#<issue>` - `<issue title>`
  - Why it matches: `<brief evidence-based reasoning>`

## Failures likely caused by this PR
- `<leg or build>` - `<short failure summary>`
  - Why: `<brief evidence-based reasoning>`
  - Evidence: `<log, timeline, or check detail>`

## Failures likely not caused by this PR
- `<leg or build>` - `<short failure summary>`
  - Why: `<brief evidence-based reasoning>`
  - Evidence: `<log, timeline, or check detail>`

## Failures that need more investigation
- `<leg or build>` - `<short failure summary>`
  - Why unclear: `<what is missing or conflicting>`
  - Evidence: `<log, timeline, or check detail>`

## Build Analysis gaps
- `<summary of each miss, exclusion, or disagreement>`
````

Guidance:

- Omit empty sections.
- Keep the comment concise but concrete.
- Link to the most relevant build, timeline, or log when possible.
- Make it obvious which failures were Build Analysis misses versus excluded
  pipelines.
- Mention **not handled** items only if they are important to the PR's CI
  picture.

## Step 7: Ask for explicit approval before posting

Before posting the PR comment, show the user:

1. the proposed comment body,
2. a short summary of how many failures landed in each bucket,
3. a reminder that the comment will be posted under the authenticated user's
   GitHub account,
4. a reminder that the comment is AI-generated.

Ask for explicit confirmation before posting.

If the authenticated user is not the PR author, say so explicitly in the
approval prompt.

If the user declines, stop after presenting the draft.

## Step 8: Post exactly one PR comment when approved

If the user explicitly approves, post the drafted comment as **one regular PR
comment** on the target PR.

Do not split the results across multiple comments.

If the skill is rerun later for the same PR, post a **new** AI-generated
comment for that run instead of editing a previous one.

## Step 9: Final response format

After the run, report:

### 1. Comment status

State one of:

- comment posted,
- draft prepared but not posted,
- no in-scope failures found,
- analysis incomplete.

### 2. Failure summary

For each handled candidate, provide:

- failing leg / build context,
- short failure summary,
- whether Build Analysis missed it or excluded it,
- assessment (`already tracked problem`, `likely PR-caused`, `likely not PR-caused`, or `unclear`),
- matched issue number and URL when applicable,
- brief reasoning.

### 3. Not handled items

For every unhandled failure, provide:

- failing leg / build context,
- why it was not handled,
- what evidence or follow-up would be needed.

## Important rules

- Do **not** create KBEs, issues, or issue drafts.
- Do **not** treat a weakly related issue as a confirmed match.
- Do **not** imply certainty when the evidence is weak.
- Do **not** post the PR comment without explicit user confirmation.
- Do **not** post a PR comment when there are no failures in scope.
- Keep the skill focused on `dotnet/xharness`.
