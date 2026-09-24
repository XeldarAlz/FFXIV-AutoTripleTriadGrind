# Contributing

Thanks for taking an interest. This is a small solo project, but PRs are welcome and I'll review them.

## Quick start

```bash
git clone --recurse-submodules https://github.com/XeldarAlz/FFXIV-AutoTripleTriadGrind.git
cd FFXIV-AutoTripleTriadGrind
dotnet build AutoTripleTriadGrind.sln -c Release
```

You need the .NET 10 SDK. The plugin requires Dalamud at runtime; CI pulls a Dalamud dev build automatically and that's enough to compile. See `.github/workflows/release.yml` if you want to reproduce CI locally.

Load the built plugin via `/xlsettings` -> **Experimental** -> **Dev Plugin Locations**, pointing at `AutoTripleTriadGrind/bin/Release/AutoTripleTriadGrind.dll`.

## Project layout

- `AutoTripleTriadGrind/Core/`: Triple Triad data from the game sheets, the match solver and deck optimizer, the route planner, automation state machine, IPC adapters.
- `AutoTripleTriadGrind/Windows/`: ImGui main window, settings, dependencies.
- `AutoTripleTriadGrind/`: plugin entry points, config, command wiring.
- `ECommons/`: submodule, shared Dalamud helpers. Don't patch this directly; upstream it.

Keep logic small and direct. This plugin has one job.

## Before you open a PR

1. `dotnet build -c Release` cleanly.
2. Test in-game against at least one NPC per rule set you touched. Rules and regional rules change how a match plays, so a fix that works against a plain NPC may not work against one playing Sudden Death or Swap.
3. Keep the diff focused. One concern per PR.
4. Match the existing style. No heavy abstractions "for later."
5. If your change affects what a user sees or types (commands, window layout, settings), update the README.
6. If you used AI beyond autocomplete, say which level in the PR description. It is one line, and [AI-USAGE.md](AI-USAGE.md) explains the level names and why I ask.

## Good first issues

Check the tracker for anything labeled `good first issue`. NPC-specific quirks (an NPC the plugin can't reach, a menu it can't get through, a rule it misplays) are usually the lowest-friction way to help: pick the NPC that's misbehaving, attach a log of what the plugin did vs. what should have happened, and a fix is usually a small change.

## Security

Please don't file public issues for security problems; see [SECURITY.md](SECURITY.md).

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Be decent.

## License

By contributing, you agree your contributions are licensed under AGPL-3.0-or-later with the additional terms in [NOTICE](NOTICE), the same as the project.
