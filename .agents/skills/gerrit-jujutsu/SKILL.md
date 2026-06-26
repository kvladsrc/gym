---
name: gerrit-jujutsu
description: "Use when working with Gerrit reviews through Jujutsu/jj: checking repository state, fetching or rebasing changes, writing non-interactive descriptions, cleaning up merged local changes, and uploading reviews with jj gerrit upload."
---

# Gerrit Jujutsu

Use this skill for Gerrit review workflows in repositories managed with
Jujutsu (`jj`). Prefer `jj` over `git` for history operations unless the user
explicitly asks for raw Git.

For this user's `src` monorepo, the reliable command form is:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c jj <command>
```

If a plain `jj ...` command is not on `PATH`, immediately retry with the Nix
form. Do not spend time looking for shell aliases before trying the Nix form.

## Happy Path

For the common request "describe the current commit and send it to Gerrit":

```sh
/nix/var/nix/profiles/default/bin/nix develop -c jj status
/nix/var/nix/profiles/default/bin/nix develop -c jj log -n 20
/nix/var/nix/profiles/default/bin/nix develop -c jj show --summary
/nix/var/nix/profiles/default/bin/nix develop -c jj desc --message "<message>"
/nix/var/nix/profiles/default/bin/nix develop -c jj gerrit upload -r @
```

Use `-r @` when the user asks for the current change/commit. Use a broader
revset only after the graph shows that the intended upload is a stack.

## First Checks

Start by discovering the repository state before changing history:

```sh
jj status
jj log -n 20
jj git remote list
```

For this user's `src` monorepo, run those checks through Nix if needed:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c jj <command>
```

Network operations such as fetch and upload may be blocked in sandboxed agent
sessions. If a required `jj git fetch` or `jj gerrit upload` fails with a
likely network/sandbox error, rerun the same command outside the sandbox
(accepting the permission prompt if one appears) instead of switching tools.

## Fetch and Rebase

Fetch through Jujutsu:

```sh
jj git fetch
jj git fetch --remote origin
jj git fetch --all-remotes
```

After fetch, inspect the new graph before rebasing:

```sh
jj log -n 30
```

When the user says that the previous Gerrit CL was merged and asks for a new
CL, do not continue editing the old local change by title alone. Fetch first,
inspect the graph, and make the next review a fresh change whose parent is the
current `master@origin` unless the user explicitly wants a stack:

```sh
jj git fetch
jj log -n 30
jj status
```

If the old local change is already merged upstream, either abandon it after
verifying it is stale, or create/rebase the intended new change onto
`master@origin`. Before upload, `jj show --summary` should show only the files
for the new review and `jj log` should show `@-` at `master@origin` for a
standalone CL.

Use `jj rebase` for moving local work onto the desired destination. Common
forms:

```sh
jj rebase -d <destination>
jj rebase -b <change-or-branch> -d <destination>
jj rebase -s <root-change> -d <destination>
jj rebase -r <single-change> -d <destination>
```

Guidance:

- If the user says "rebase my stack/current work", default to `jj rebase -d
  <destination>` from the current change after confirming the destination from
  `jj log`.
- Use `-b` for a whole local stack/branch relative to a destination.
- Use `-s` for a root change and its descendants.
- Use `-r` only for selected individual revisions without automatically moving
  their descendants.
- After rebasing, run `jj status` and `jj log -n 20` to show the resulting
  stack and conflicts.

## Cleaning Merged Local Changes

Sometimes local changes remain after Gerrit has merged them. Do not delete
anything based only on a title match. First fetch and inspect whether the local
change is already present upstream or has an identical Gerrit/Change-Id result:

```sh
jj git fetch
jj log -n 40
jj show <local-change>
```

When the user confirms that a local change is already merged, remove it with:

```sh
jj abandon <local-change>
```

Use `jj abandon` instead of Git reset/checkout. If multiple local changes are
stale, abandon an explicit revset and report exactly what was removed:

```sh
jj abandon '<revset>'
```

After cleanup, verify:

```sh
jj status
jj log -n 20
```

If the wrong change was abandoned, recovery is usually possible via `jj op log`
and `jj op restore`, so do not run destructive Git recovery commands.

## Change Descriptions

Avoid interactive editors in agent workflows. `jj desc` is an alias for
`jj describe`; use `--message` for short descriptions and `--stdin` for
multi-line messages:

```sh
jj desc -m "Short subject"
printf '%s\n' "Short subject" "" "Body paragraph." | jj desc --stdin
```

When writing a Gerrit review message:

- Put a concise subject on the first line.
- Add a blank line before the body when a body is useful.
- Explain the behavioral change and why it is needed.
- Mention tests or validation in the body when relevant.
- Keep body lines under 72 characters because Gerrit warns on long commit
  message lines.
- Preserve existing Gerrit metadata such as `Change-Id:` if it already exists.
- Do not invent `Change-Id:` lines. Let the configured Gerrit hook/tooling or
  `jj gerrit upload` handle them unless repository conventions clearly require
  otherwise.

Before changing a description, inspect the current change:

```sh
jj show
```

After changing it, verify with:

```sh
jj show --summary
```

## Uploading to Gerrit

Use the Gerrit subcommand that is available in this repository:

```sh
jj gerrit upload -r @
```

Do not try `jj upload` first in this repository; it is not a registered
subcommand. If upload flags are needed, inspect:

```sh
jj gerrit upload --help
```

Before uploading:

```sh
jj status
jj log -n 20
jj show --summary
jj diff --summary
```

Upload only the intended change or stack. Prefer explicit revisions over broad
defaults when the graph contains unrelated mutable work:

```sh
jj gerrit upload -r @
jj gerrit upload -r '<explicit-revset>'
```

If Gerrit warns about long message lines, rewrite the description with wrapped
body lines and upload again. A message-only update to the same `Change-Id`
updates the existing Gerrit review.

After upload, report the Gerrit URL/change identifiers printed by the command
and the final `jj status` summary.

Sometimes `jj gerrit upload` succeeds but does not print a Gerrit URL. In that
case, do not scan all `refs/changes/*` unless there is no better option: first
use Gerrit query/API tooling if available, or inspect a narrow recent refs
range. Remember that the commit object in Gerrit can differ from the local
commit if upload tooling adds a `Change-Id` footer, so matching by local commit
hash can fail. Matching by subject, `Change-Id`, and changed files is more
reliable.

## Safety Rules

- Do not run `git reset --hard`, `git checkout --`, or similar destructive Git
  commands for `jj` cleanup.
- Do not rewrite immutable or remote-tracking history unless the user explicitly
  asks and the command requires it.
- Before `jj abandon`, `jj rebase`, or `jj gerrit upload`, identify the target
  revisions in plain language.
- If a command opens an editor or would require interaction, stop and switch to
  a non-interactive form such as `jj desc --stdin`.
- Keep Gerrit and Jujutsu operations scoped to the repository requested by the
  user.
