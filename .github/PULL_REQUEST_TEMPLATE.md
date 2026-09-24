## What

<!-- One or two sentences on what this PR changes. -->

## Why

<!-- The motivating problem, linked issue, or user-visible behavior this fixes. -->

Closes #

## How to test

<!--
Minimum steps a reviewer can run to verify the change. Testing usually means picking one missing card from an unlocked NPC, pressing Start, and watching the plugin travel there, play, and register the card end to end. Make sure the dependencies listed in /attg deps are installed first. For UI-only changes, describe what to click.
-->

## Checklist

- [ ] `dotnet build -c Release` passes
- [ ] Verified in-game against at least one NPC with the affected rules
- [ ] If this changes user-visible behavior, README is updated
- [ ] If this touches the automation loop, relevant `[AutoTripleTriadGrind]` log lines make the sequence auditable
